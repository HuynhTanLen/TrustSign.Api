using TrustSign.Domain.Entities.Audit;

namespace TrustSign.Api.Tests;

/// <summary>Chuỗi băm nhật ký (AUD-01, F11): chuỗi nguyên vẹn qua kiểm tra, sửa hoặc xoá một sự kiện thì bị phát hiện.</summary>
public sealed class AuditChainTests
{
    private static List<AuditEvent> Chain(int length)
    {
        List<AuditEvent> chain = [];
        DateTimeOffset at = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);
        for (int i = 0; i < length; i++)
        {
            // Cùng một mốc thời gian: thứ tự phải đến từ PrevHash, không phải CreatedAt.
            chain.Add(AuditEvent.Create(null, null, null, $"contract.step{i}", "Contract", "c1", $"{{\"Detail\":\"{i}\"}}", chain.LastOrDefault()?.Hash, at));
        }

        return chain;
    }

    [Fact]
    public void Intact_chain_verifies_and_is_ordered_by_links()
    {
        List<AuditEvent> chain = Chain(4);
        Assert.Equal(AuditEvent.GenesisHash, chain[0].PrevHash);

        IReadOnlyList<AuditEvent> shuffled = AuditEvent.OrderChain([chain[2], chain[0], chain[3], chain[1]]);

        Assert.Equal(chain, shuffled);
        Assert.True(AuditEvent.VerifyChain(shuffled));
    }

    [Fact]
    public void Edited_payload_breaks_the_chain()
    {
        List<AuditEvent> chain = Chain(3);
        typeof(AuditEvent).GetProperty(nameof(AuditEvent.PayloadJson))!.SetValue(chain[1], "{\"Detail\":\"sửa\"}");

        Assert.False(AuditEvent.VerifyChain(chain));
    }

    [Fact]
    public void Deleted_event_breaks_the_chain()
    {
        List<AuditEvent> chain = Chain(3);
        chain.RemoveAt(1);

        Assert.False(AuditEvent.VerifyChain(AuditEvent.OrderChain(chain)));
    }
}
