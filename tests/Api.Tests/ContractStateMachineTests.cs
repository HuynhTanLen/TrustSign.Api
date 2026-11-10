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

    // Bên A ký (1) → đầu mối bên B điều phối (2, giữ ô ký cho bên B) → Cc (3).
    private static Contract SentWithCoordinator()
    {
        Contract c = Contract.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "HD-C", "HĐ", null, "k", new string('a', 64), 100, Guid.NewGuid(), "A", Now).Value;
        Assert.True(c.SetSigners([
            new ContractSignerSpec("Giám đốc A", "gd@a.vn"),
            new ContractSignerSpec("Đầu mối B", "tu@b.vn", PageNumber: 2, BoxX: 300, BoxY: 80, BoxWidth: 170, BoxHeight: 50, Role: RecipientRole.Coordinator),
            new ContractSignerSpec("Kế toán", "kt@a.vn", Role: RecipientRole.Cc),
        ], Now).IsSuccess);
        Assert.True(c.MarkSent(Now.AddDays(3), Now).IsSuccess);
        Assert.True(c.SignOrApproveCurrent(SignerAt(c, 1), Now).IsSuccess);
        return c;
    }

    [Fact]
    public void Coordinator_inserts_own_side_after_itself_and_hands_turn_to_first_added()
    {
        Contract c = SentWithCoordinator();
        Guid coordinatorId = SignerAt(c, 2);

        Result result = c.Coordinate(coordinatorId, [
            new ContractSignerSpec("Pháp chế B", "lan@b.vn", Role: RecipientRole.Approver),
            new ContractSignerSpec("Giám đốc B", "minh@b.vn", Role: RecipientRole.Signer),
        ], Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [("gd@a.vn", SignerStatus.Signed), ("tu@b.vn", SignerStatus.Signed), ("lan@b.vn", SignerStatus.Current), ("minh@b.vn", SignerStatus.Waiting), ("kt@a.vn", SignerStatus.Waiting)],
            c.Signers.OrderBy(s => s.SignOrder).Select(s => (s.Email, s.Status)));
        Assert.Equal([1, 2, 3, 4, 5], c.Signers.OrderBy(s => s.SignOrder).Select(s => s.SignOrder));

        ContractSigner signerB = c.Signers.Single(s => s.Email == "minh@b.vn");
        Assert.Equal((2, 300f, 80f, 170f, 50f), (signerB.PageNumber, signerB.BoxX, signerB.BoxY, signerB.BoxWidth, signerB.BoxHeight));

        Assert.True(c.SignOrApproveCurrent(SignerAt(c, 3), Now).IsSuccess);
        Assert.True(c.SignOrApproveCurrent(SignerAt(c, 4), Now).IsSuccess);
        Assert.Equal(ContractStatus.Completed, c.Status);
    }

    [Fact]
    public void Coordinator_cannot_sign_and_must_name_exactly_one_signer()
    {
        Contract c = SentWithCoordinator();
        Guid coordinatorId = SignerAt(c, 2);

        Assert.Equal(ContractErrors.CoordinatorMustCoordinate, c.SignOrApproveCurrent(coordinatorId, Now).Error);
        Assert.Equal(ContractErrors.InvalidCoordination, c.Coordinate(coordinatorId, [new ContractSignerSpec("Duyệt", "d@b.vn", Role: RecipientRole.Approver)], Now).Error);
        Assert.Equal(ContractErrors.InvalidCoordination, c.Coordinate(coordinatorId, [new ContractSignerSpec("K1", "k1@b.vn"), new ContractSignerSpec("K2", "k2@b.vn")], Now).Error);
        Assert.Equal(ContractErrors.InvalidCoordination, c.Coordinate(coordinatorId, [new ContractSignerSpec("Đ", "d@b.vn", Role: RecipientRole.Coordinator), new ContractSignerSpec("K", "k@b.vn")], Now).Error);
        Assert.Equal(ContractErrors.InvalidSignerEmail(3), c.Coordinate(coordinatorId, [new ContractSignerSpec("K", "sai-email")], Now).Error);
        // Vượt tối đa 5 người nhận (đang có 3).
        Assert.Equal(ContractErrors.InvalidCoordination, c.Coordinate(coordinatorId, [
            new ContractSignerSpec("D1", "d1@b.vn", Role: RecipientRole.Approver),
            new ContractSignerSpec("D2", "d2@b.vn", Role: RecipientRole.Approver),
            new ContractSignerSpec("K", "k@b.vn"),
        ], Now).Error);

        Assert.Equal(3, c.Signers.Count);
        Assert.Equal(SignerStatus.Current, c.Signers.Single(s => s.Id == coordinatorId).Status);
        Assert.Equal(ContractErrors.NotCoordinator, c.Coordinate(SignerAt(c, 1), [new ContractSignerSpec("K", "k@b.vn")], Now).Error);
    }

    [Fact]
    public void Contract_with_only_coordinator_can_be_sent()
    {
        Contract c = Contract.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "HD-D", "HĐ", null, "k", new string('a', 64), 100, Guid.NewGuid(), "A", Now).Value;
        Assert.True(c.SetSigners([new ContractSignerSpec("Đầu mối B", "tu@b.vn", Role: RecipientRole.Coordinator)], Now).IsSuccess);
        Assert.True(c.MarkSent(Now.AddDays(3), Now).IsSuccess);
        Assert.Equal(SignerStatus.Current, c.Signers[0].Status);
    }
}
