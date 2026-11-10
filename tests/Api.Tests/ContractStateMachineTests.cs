using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Domain.Entities.Contracts;

namespace TrustSign.Api.Tests;

/// <summary>Bảng chuyển trạng thái hợp đồng và lượt ký (SIG-02, TC05), kiểm thẳng trên entity.</summary>
public sealed class ContractStateMachineTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 9, 0, 0, TimeSpan.Zero);

    private static Contract SentContract(int signers = 3)
    {
        Contract c = Contract.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "HD-1", "HĐ", null, "k", new string('a', 64), 100, Guid.NewGuid(), "A", Now).Value;
        c.SetSigners([.. Enumerable.Range(1, signers).Select(i => new ContractSignerSpec($"Người {i}", $"n{i}@x.vn"))], Now);
        Assert.True(c.MarkSent(Now.AddDays(3), Now).IsSuccess);
        return c;
    }

    private static Guid SignerAt(Contract c, int order) => c.Signers.Single(s => s.SignOrder == order).Id;

    [Fact]
    public void Three_signers_sign_in_order_then_contract_completes()
    {
        Contract c = SentContract();
        Assert.Equal(ContractStatus.Pending, c.Status);
        Assert.Equal([SignerStatus.Current, SignerStatus.Waiting, SignerStatus.Waiting], c.Signers.Select(s => s.Status));

        Assert.True(c.SignOrApproveCurrent(SignerAt(c, 1), Now).IsSuccess);
        Assert.Equal(ContractStatus.Signing, c.Status);
        Assert.True(c.SignOrApproveCurrent(SignerAt(c, 2), Now).IsSuccess);
        Assert.True(c.SignOrApproveCurrent(SignerAt(c, 3), Now).IsSuccess);

        Assert.Equal(ContractStatus.Completed, c.Status);
        Assert.Equal(Now, c.CompletedAt);
        Assert.All(c.Signers, s => Assert.Equal(SignerStatus.Signed, s.Status));
    }

    [Fact]
    public void Signer_out_of_turn_is_rejected_TC05()
    {
        Contract c = SentContract();

        Result early = c.SignOrApproveCurrent(SignerAt(c, 2), Now);

        Assert.Equal(ContractErrors.NotSignerTurn, early.Error);
        Assert.Equal(SignerStatus.Waiting, c.Signers.Single(s => s.SignOrder == 2).Status);
    }

    [Fact]
    public void Draft_cannot_be_signed()
    {
        Contract c = Contract.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "HD-2", "HĐ", null, "k", new string('a', 64), 100, Guid.NewGuid(), "A", Now).Value;
        c.SetSigners([new ContractSignerSpec("Người 1", "n1@x.vn")], Now);

        Assert.Equal(ContractErrors.ContractNotEditable, c.SignOrApproveCurrent(c.Signers[0].Id, Now).Error);
    }

    [Fact]
    public void Decline_ends_flow_and_blocks_later_signing()
    {
        Contract c = SentContract();
        c.SignOrApproveCurrent(SignerAt(c, 1), Now);

        Assert.True(c.DeclineCurrent(SignerAt(c, 2), "Sai giá", Now).IsSuccess);

        Assert.Equal(ContractStatus.Declined, c.Status);
        Assert.Equal(ContractErrors.ContractNotEditable, c.SignOrApproveCurrent(SignerAt(c, 3), Now).Error);
        Assert.Equal(ContractErrors.ContractCannotCancel, c.Cancel(Now).Error);
    }

    [Fact]
    public void Cancel_is_allowed_while_signing_but_not_after_completion()
    {
        Contract signing = SentContract();
        signing.SignOrApproveCurrent(SignerAt(signing, 1), Now);
        Assert.True(signing.Cancel(Now).IsSuccess);
        Assert.Equal(ContractStatus.Cancelled, signing.Status);

        Contract done = SentContract(signers: 1);
        done.SignOrApproveCurrent(SignerAt(done, 1), Now);
        Assert.Equal(ContractErrors.ContractCannotCancel, done.Cancel(Now).Error);
    }

    [Fact]
    public void Expires_only_after_due_date_and_only_while_unfinished()
    {
        Contract c = SentContract();
        Assert.False(c.MarkExpiredIfDue(Now.AddDays(2)));
        Assert.True(c.MarkExpiredIfDue(Now.AddDays(3)));
        Assert.Equal(ContractStatus.Expired, c.Status);

        Contract done = SentContract(signers: 1);
        done.SignOrApproveCurrent(SignerAt(done, 1), Now);
        Assert.False(done.MarkExpiredIfDue(Now.AddDays(10)));
        Assert.Equal(ContractStatus.Completed, done.Status);
    }
}
