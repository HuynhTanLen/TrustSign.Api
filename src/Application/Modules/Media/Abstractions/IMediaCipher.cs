namespace TrustSign.Application.Modules.Media;

/// <summary>Mã hóa AES-256-GCM cho file nhạy cảm (ảnh CCCD, ảnh khuôn mặt). Định dạng lưu: nonce (12) | tag (16) | bản mã.</summary>
public interface IMediaCipher
{
    byte[] Encrypt(byte[] plain);

    byte[] Decrypt(byte[] sealedBytes);
}
