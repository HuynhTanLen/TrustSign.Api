using Microsoft.Extensions.DependencyInjection;

namespace TrustSign.Application.Modules.Media;

/// <summary>
/// Module Media: mọi thứ liên quan file và ảnh: kho lưu, ký token/URL xem file, nhận dạng loại file, mã hóa file nhạy cảm.
/// Module khác chỉ lưu và đọc byte qua đây; nghiệp vụ "ai được xem file nào" vẫn ở module sở hữu file.
/// </summary>
public static class MediaModule
{
    public static IServiceCollection AddMediaModule(this IServiceCollection services)
    {
        services.AddOptions<MediaOptions>().BindConfiguration(MediaOptions.SectionName);

        services.AddSingleton<IMediaStorage, LocalMediaStorage>();
        services.AddSingleton<IMediaSigner, HmacMediaSigner>();
        services.AddSingleton<IMediaCipher, AesMediaCipher>();

        return services;
    }
}
