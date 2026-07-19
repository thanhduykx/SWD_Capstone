using System;
using Npgsql;

const string connString = "Host=aws-0-ap-southeast-1.pooler.supabase.com.;Port=5432;Database=postgres;Username=postgres.abvfaooyxmpufzzanuya;Password=EXE101@gmail.com;Ssl Mode=Require;Trust Server Certificate=true";

await using var conn = new NpgsqlConnection(connString);
await conn.OpenAsync();

Console.WriteLine("=== STUDENTS IN GROUP 6 ===");
await using var cmd = new NpgsqlCommand("SELECT s.id, s.user_id, s.code, s.is_leader, u.username, u.email FROM students s JOIN users u ON s.user_id = u.id WHERE s.group_id = 6", conn);
await using var r = await cmd.ExecuteReaderAsync();
while (await r.ReadAsync())
{
    Console.WriteLine($"Student ID={r.GetInt32(0)}, UserId={r.GetInt32(1)}, Code={r.GetString(2)}, IsLeader={r.GetBoolean(3)}, Username={r.GetString(4)}, Email={r.GetString(5)}");
}
await r.CloseAsync();
