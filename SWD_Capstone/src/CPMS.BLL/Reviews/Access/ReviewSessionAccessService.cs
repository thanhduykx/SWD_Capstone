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
        int generatedByUserId,
        CancellationToken cancellationToken)
    {
        ReviewSessionAccessState state = await RequireSessionAsync(sessionId, cancellationToken);
        string accessCode = RandomNumberGenerator.GetString(AccessCodeAlphabet, AccessCodeLength);
        string accessCodeHash = passwordHasher.Hash(accessCode);
        int codeVersion = checked(state.CodeVersion + 1);
        DateTime generatedAt = DateTime.UtcNow;

        await store.SaveAccessCodeAsync(
            sessionId,
            accessCodeHash,
            codeVersion,
            generatedByUserId,
            generatedAt,
            cancellationToken);

        return new GeneratedReviewSessionAccessCode(
            sessionId,
            accessCode,
            codeVersion,
            generatedAt);
    }

    public async Task<VerifiedReviewSessionAccess> VerifyAsync(
        int sessionId,
        int lecturerUserId,
        string accessCode,
        CancellationToken cancellationToken)
    {
        string normalizedCode = NormalizeCode(accessCode);
        ReviewSessionAccessState state = await RequireSessionAsync(sessionId, cancellationToken);
        int lecturerId = await store.FindAssignedLecturerAsync(
            sessionId,
            lecturerUserId,
            cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Only a lecturer assigned to this review session can enter its access code.");

        if (string.IsNullOrWhiteSpace(state.AccessCodeHash))
        {
            throw new BusinessRuleException(
                "The Training Department has not generated an access code for this review session.");
        }
        if (!passwordHasher.Verify(state.AccessCodeHash, normalizedCode))
        {
            throw new BusinessRuleException("The review session access code is incorrect.");
        }

        DateTime verifiedAt = DateTime.UtcNow;
        await store.SaveGrantAsync(
            sessionId,
            lecturerId,
            state.CodeVersion,
            verifiedAt,
            cancellationToken);
        return new VerifiedReviewSessionAccess(sessionId, true, verifiedAt);
    }

    public async Task EnsureLecturerAccessAsync(
        int sessionId,
        int lecturerUserId,
        CancellationToken cancellationToken)
    {
        ReviewSessionAccessState state = await RequireSessionAsync(sessionId, cancellationToken);
        int lecturerId = await store.FindAssignedLecturerAsync(
            sessionId,
            lecturerUserId,
            cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "You are not assigned to this review session.");

        if (string.IsNullOrWhiteSpace(state.AccessCodeHash)
            || !await store.HasCurrentGrantAsync(
                sessionId,
                lecturerId,
                state.CodeVersion,
                cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Enter the correct review session access code before opening this session.");
        }
    }

    private async Task<ReviewSessionAccessState> RequireSessionAsync(
        int sessionId,
        CancellationToken cancellationToken) =>
        await store.FindSessionAsync(sessionId, cancellationToken)
        ?? throw new KeyNotFoundException("Review session not found.");

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
