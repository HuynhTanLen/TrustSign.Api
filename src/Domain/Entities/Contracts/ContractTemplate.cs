using System.Text.Json;
using System.Text.RegularExpressions;
using TrustSign.Common;

namespace TrustSign.Domain.Entities.Contracts;

/// <summary>
/// Mẫu hợp đồng dùng chung trong phạm vi một doanh nghiệp (F21, F22).
/// Các trường động viết theo cú pháp <c>{{ten_bien}}</c> và được trích xuất tự động theo thứ tự xuất hiện.
/// </summary>
public sealed partial class ContractTemplate
{
    private static readonly Dictionary<string, string> FriendlyLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ten_ben_a"] = "Tên doanh nghiệp (Bên A)",
        ["mst_ben_a"] = "Mã số thuế Bên A",
        ["dai_dien_ben_a"] = "Người đại diện Bên A",
        ["ten_ben_b"] = "Tên đối tác / Khách hàng (Bên B)",
        ["mst_ben_b"] = "Mã số thuế Bên B",
        ["dai_dien_ben_b"] = "Người đại diện Bên B",
        ["gia_tri_hop_dong"] = "Giá trị hợp đồng (VNĐ)",
        ["ngay_hieu_luc"] = "Ngày hiệu lực",
        ["thoi_han_hop_dong"] = "Thời hạn hợp đồng",
        ["ho_ten_nhan_vien"] = "Họ và tên nhân viên",
        ["cccd_nhan_vien"] = "Số CCCD nhân viên",
        ["vi_tri_cong_viec"] = "Vị trí công việc",
        ["muc_luong_chinh"] = "Mức lương chính (VNĐ/tháng)",
        ["muc_dich_bao_mat"] = "Mục đích trao đổi thông tin",
        ["so_hop_dong_goc"] = "Số hợp đồng gốc"
    };

    private ContractTemplate()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = null!;

    public string Content { get; private set; } = null!;

    /// <summary>Danh sách tên biến lưu dạng mảng JSON để giữ nguyên thứ tự xuất hiện.</summary>
    public string VariablesJson { get; private set; } = "[]";

    public Guid CreatedByUserId { get; private set; }

    public string AuthorName { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<ContractTemplate> Create(
        Guid tenantId,
        string name,
        string content,
        Guid createdByUserId,
        string authorName,
        DateTimeOffset now)
    {
        string trimmedName = name?.Trim() ?? string.Empty;
        string trimmedContent = content?.Trim() ?? string.Empty;

        if (trimmedName.Length is 0 or > 200 || trimmedContent.Length == 0)
        {
            return ContractErrors.InvalidTemplate;
        }

        IReadOnlyList<string> variables = ExtractVariables(trimmedContent);

        return new ContractTemplate
        {
            Id = Guid.CreateVersion7(now),
            TenantId = tenantId,
            Name = trimmedName,
            Content = trimmedContent,
            VariablesJson = JsonSerializer.Serialize(variables),
            CreatedByUserId = createdByUserId,
            AuthorName = string.IsNullOrWhiteSpace(authorName) ? "Thành viên" : authorName.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public Result Update(string name, string content, DateTimeOffset now)
    {
        string trimmedName = name?.Trim() ?? string.Empty;
        string trimmedContent = content?.Trim() ?? string.Empty;

        if (trimmedName.Length is 0 or > 200 || trimmedContent.Length == 0)
        {
            return ContractErrors.InvalidTemplate;
        }

        IReadOnlyList<string> variables = ExtractVariables(trimmedContent);
        Name = trimmedName;
        Content = trimmedContent;
        VariablesJson = JsonSerializer.Serialize(variables);
        UpdatedAt = now;

        return Result.Success();
    }

    public IReadOnlyList<string> GetVariables()
    {
        if (string.IsNullOrWhiteSpace(VariablesJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(VariablesJson) ?? [];
    }

    /// <summary>
    /// Điền giá trị các biến vào nội dung mẫu để sinh văn bản hợp đồng (F22, TC19).
    /// </summary>
    public Result<string> Render(IReadOnlyDictionary<string, string>? values)
    {
        IReadOnlyList<string> requiredVars = GetVariables();
        foreach (string variable in requiredVars)
        {
            if (values is null ||
                !values.TryGetValue(variable, out string? val) ||
                string.IsNullOrWhiteSpace(val))
            {
                return ContractErrors.MissingTemplateVariable;
            }
        }

        string templateSource = Content;
        string withoutVars = VariablePlaceholderRegex().Replace(templateSource, string.Empty).Trim();
        if (withoutVars.Length < 15 && requiredVars.Count > 1)
        {
            IEnumerable<string> structuredLines = requiredVars.Select(v =>
            {
                string label = FriendlyLabels.TryGetValue(v, out string? friendly)
                    ? friendly
                    : v.Replace('_', ' ');
                return $"- {label}: {{{{{v}}}}}";
            });
            templateSource =
                "Các bên tham gia giao kết hợp đồng với thông tin và điều khoản cụ thể như sau:\n" +
                string.Join("\n", structuredLines) +
                "\n\nHai bên cam kết thực hiện đúng các nội dung đã thỏa thuận nêu trên.";
        }

        string rendered = VariablePlaceholderRegex().Replace(templateSource, match =>
        {
            string varName = match.Groups[1].Value;
            return values is not null && values.TryGetValue(varName, out string? v)
                ? v.Trim()
                : match.Value;
        });

        return rendered;
    }

    public static IReadOnlyList<string> ExtractVariables(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        List<string> result = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (Match match in VariablePlaceholderRegex().Matches(content))
        {
            string name = match.Groups[1].Value;
            if (seen.Add(name))
            {
                result.Add(name);
            }
        }

        return result;
    }

    [GeneratedRegex(@"\{\{([a-zA-Z0-9_]+)\}\}")]
    private static partial Regex VariablePlaceholderRegex();
}
