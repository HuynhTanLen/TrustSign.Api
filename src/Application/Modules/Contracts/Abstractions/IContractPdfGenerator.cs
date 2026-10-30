namespace TrustSign.Application.Modules.Contracts;

public interface IContractPdfGenerator
{
    /// <summary>
    /// Kết xuất nội dung văn bản hợp đồng (đã điền biến) thành file PDF chuẩn A4 có tiếng Việt Unicode bằng iText 9 (F22, TC19).
    /// </summary>
    byte[] RenderTemplateToPdf(string title, string contractCode, string renderedContent, DateTimeOffset now);
}
