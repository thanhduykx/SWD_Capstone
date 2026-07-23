using System.Security.Cryptography;
using CPMS.BLL.Abstractions.Security;
using CPMS.BLL.Exceptions;

namespace CPMS.BLL.Reviews.Access;

public sealed class ReviewSessionAccessService(
    IReviewSessionAccessStore store,
    IPasswordHasher passwordHasher) : IReviewSessionAccessService
{
    private const int AccessCodeLength = 8;
    private const string AccessCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<GeneratedReviewSessionAccessCode> GenerateAsync(
        int sessionId,
        int groupId,
        int generatedByUserId,
        CancellationToken cancellationToken)
    {
        ReviewSessionAccessState state = await RequireGroupAsync(
            sessionId, groupId, cancellationToken);
        int codeVersion = checked(state.CodeVersion + 1);
        string accessCode = RandomNumberGenerator.GetString(AccessCodeAlphabet, AccessCodeLength);
        string accessCodeHash = passwordHasher.Hash(accessCode);
        DateTime generatedAt = DateTime.UtcNow;
        await store.SaveAccessCodeAsync(
            sessionId,
            groupId,
            accessCode,
            accessCodeHash,
            codeVersion,
            generatedByUserId,
            generatedAt,
            cancellationToken);

        return new GeneratedReviewSessionAccessCode(
            sessionId,
            groupId,
            accessCode,
            codeVersion,
            generatedAt);
    }

    public async Task<VerifiedReviewSessionAccess> VerifyAsync(
        int sessionId,
        int groupId,
        int lecturerUserId,
        string accessCode,
        CancellationToken cancellationToken)
    {
        string normalizedCode = NormalizeCode(accessCode);
        ReviewSessionAccessState state = await RequireGroupAsync(
            sessionId, groupId, cancellationToken);
        int lecturerId = await store.FindAssignedLecturerAsync(
            sessionId,
            groupId,
            lecturerUserId,
            cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Only a lecturer assigned to this review group can enter its access code.");

        if (string.IsNullOrWhiteSpace(state.AccessCodeHash))
        {
            throw new BusinessRuleException(
                "The Training Department has not generated an access code for this review group.");
        }
        if (!passwordHasher.Verify(state.AccessCodeHash, normalizedCode))
        {
            throw new BusinessRuleException("The review group access code is incorrect.");
        }

        DateTime verifiedAt = DateTime.UtcNow;
        await store.SaveGrantAsync(
            sessionId,
            groupId,
            lecturerId,
            state.CodeVersion,
            verifiedAt,
            cancellationToken);
        return new VerifiedReviewSessionAccess(sessionId, groupId, true, verifiedAt);
    }

    public async Task EnsureLecturerAccessAsync(
        int sessionId,
        int groupId,
        int lecturerUserId,
        CancellationToken cancellationToken)
    {
        ReviewSessionAccessState state = await RequireGroupAsync(
            sessionId, groupId, cancellationToken);
        int lecturerId = await store.FindAssignedLecturerAsync(
            sessionId,
            groupId,
            lecturerUserId,
            cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "You are not assigned to this review group.");

        if (string.IsNullOrWhiteSpace(state.AccessCodeHash)
            || !await store.HasCurrentGrantAsync(
                sessionId,
                groupId,
                lecturerId,
                state.CodeVersion,
                cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Enter the correct review group access code before opening this group.");
        }
    }

    public async Task<GeneratedReviewSessionAccessCode> GenerateForSessionAsync(
        int sessionId,
        int generatedByUserId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<int> groupIds = await store.FindGroupIdsInSessionAsync(
            sessionId, cancellationToken);
        if (groupIds.Count == 0)
        {
            throw new KeyNotFoundException("Review session has no groups to lock with an access code.");
        }

        string accessCode = RandomNumberGenerator.GetString(AccessCodeAlphabet, AccessCodeLength);
        string accessCodeHash = passwordHasher.Hash(accessCode);
        DateTime generatedAt = DateTime.UtcNow;
        int lastVersion = 0;
        int firstGroupId = groupIds[0];

        foreach (int groupId in groupIds)
        {
            ReviewSessionAccessState state = await RequireGroupAsync(
                sessionId, groupId, cancellationToken);
            int codeVersion = checked(state.CodeVersion + 1);
            lastVersion = codeVersion;
            await store.SaveAccessCodeAsync(
                sessionId,
                groupId,
                accessCode,
                accessCodeHash,
                codeVersion,
                generatedByUserId,
                generatedAt,
                cancellationToken);
        }

        return new GeneratedReviewSessionAccessCode(
            sessionId,
            firstGroupId,
            accessCode,
            lastVersion,
            generatedAt);
    }

    public async Task<VerifiedReviewSessionAccess> VerifyForSessionAsync(
        int sessionId,
        int lecturerUserId,
        string accessCode,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<int> groupIds = await store.FindAssignedGroupIdsAsync(
            sessionId, lecturerUserId, cancellationToken);
        if (groupIds.Count == 0)
        {
            throw new UnauthorizedAccessException(
                "Only a lecturer assigned to this review session can enter its access code.");
        }

        BusinessRuleException? lastCodeError = null;
        foreach (int groupId in groupIds)
        {
            try
            {
                return await VerifyAsync(
                    sessionId, groupId, lecturerUserId, accessCode, cancellationToken);
            }
            catch (UnauthorizedAccessException)
            {
                // Thử nhóm kế tiếp nếu panel/submission lệch từng nhóm.
            }
            catch (BusinessRuleException exception)
            {
                lastCodeError = exception;
            }
        }

        throw lastCodeError is not null
            ? lastCodeError
            : new UnauthorizedAccessException(
                "Only a lecturer assigned to this review session can enter its access code.");
    }

    private async Task<ReviewSessionAccessState> RequireGroupAsync(
        int sessionId,
        int groupId,
        CancellationToken cancellationToken) =>
        await store.FindGroupAsync(sessionId, groupId, cancellationToken)
        ?? throw new KeyNotFoundException("Review group was not found in this session.");

    private static string NormalizeCode(string? accessCode)
    {
        string normalized = accessCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != AccessCodeLength
            || normalized.Any(character => !AccessCodeAlphabet.Contains(character)))
        {
            throw new BusinessRuleException(
                "The review session access code must contain exactly 8 valid characters.");
        }
        return normalized;
    }
}
