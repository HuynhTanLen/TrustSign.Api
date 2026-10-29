using System.Text;
using iText.Bouncycastle.Crypto;
using iText.Bouncycastle.X509;
using iText.Commons.Bouncycastle.Cert;
using iText.Forms.Form.Element;
using iText.IO.Font;
using iText.Kernel.Colors;
using iText.Kernel.Crypto;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Signatures;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkix;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;
using Org.BouncyCastle.X509.Store;

namespace TrustSign.Api.Tests;

/// <summary>
/// SIG-01 &amp; TC03: Spike kiểm chứng ký số PAdES nhiều bên (iText 9 + BouncyCastle) trên mô hình PKI 2 cấp:
/// Root CA -> Issuing CA -> Chứng thư người ký (Bên A, Bên B).
/// Đồng thời xuất bộ file mẫu ra <c>artifacts/pades-spike/</c> để kiểm tra trực tiếp trên Adobe Acrobat Reader và Foxit Reader.
/// </summary>
public sealed class PadesSpikeTests
{
    private const string OriginalAmountText = "100,000,000 VND";
    private const string TamperedAmountText = "900,000,000 VND";

    [Fact]
    public void TwoSequentialPadesSignatures_BothRemainValid_AndChainToRootCa()
    {
        PkiHierarchy pki = BuildTwoTierPki();
        IssuedIdentity partyA = IssueSignerCertificate(
            pki,
            commonName: "Nguyen Van An",
            email: "an.nguyen@ben-a.vn",
            organization: "Cong ty TNHH Giai Phap So Ben A");

        IssuedIdentity partyB = IssueSignerCertificate(
            pki,
            commonName: "Tran Thi Binh",
            email: "binh.tran@ben-b.vn",
            organization: "Cong ty Co Phan Thuong Mai Ben B");

        byte[] unsignedPdf = CreateSampleContractPdf();

        // Lượt ký 1 (Bên A): tạo revision 1 với chữ ký PAdES-B-B (CryptoStandard.CADES).
        byte[] signedOncePdf = SignPdfPades(
            unsignedPdf,
            partyA,
            pki,
            fieldName: "Signature_PartyA",
            rect: new Rectangle(55, 130, 220, 85),
            signerLabel: "DAI DIEN BEN A\nKy boi: Nguyen Van An\nCong ty TNHH Giai Phap So Ben A",
            reason: "Ben A dong y va ky hop dong",
            location: "TP. Ho Chi Minh, Viet Nam");

        // Lượt ký 2 (Bên B): bắt buộc dùng Append Mode (Incremental Update) để nối thêm revision 2 mà không phá chữ ký 1.
        byte[] signedTwicePdf = SignPdfPades(
            signedOncePdf,
            partyB,
            pki,
            fieldName: "Signature_PartyB",
            rect: new Rectangle(320, 130, 220, 85),
            signerLabel: "DAI DIEN BEN B\nKy boi: Tran Thi Binh\nCong ty Co Phan Thuong Mai Ben B",
            reason: "Ben B chap thuan toan bo dieu khoan",
            location: "Ha Noi, Viet Nam");

        using PdfDocument pdfDoc = new(new PdfReader(new MemoryStream(signedTwicePdf)));
        SignatureUtil sigUtil = new(pdfDoc);

        IList<string> signatureNames = sigUtil.GetSignatureNames();
        Assert.Equal(["Signature_PartyA", "Signature_PartyB"], signatureNames);
        Assert.Equal(2, sigUtil.GetTotalRevisions());
        Assert.Equal(1, sigUtil.GetRevision("Signature_PartyA"));
        Assert.Equal(2, sigUtil.GetRevision("Signature_PartyB"));
        Assert.False(sigUtil.SignatureCoversWholeDocument("Signature_PartyA"));
        Assert.True(sigUtil.SignatureCoversWholeDocument("Signature_PartyB"));

        foreach (string sigName in signatureNames)
        {
            PdfPKCS7 pkcs7 = sigUtil.ReadSignatureData(sigName);
            Assert.True(pkcs7.VerifySignatureIntegrityAndAuthenticity(), $"Chữ ký '{sigName}' phải toàn vẹn và xác thực.");

            X509Certificate signerCert = UnwrapCertificate(pkcs7.GetSigningCertificate());
            ValidateCertificateChainToRoot(signerCert, pki.IssuingCa.Certificate, pki.RootCa.Certificate);
        }

        ExportArtifactsForManualReaderInspection(pki, signedTwicePdf);
    }

    [Fact]
    public void TamperingSingleByteInSignedPdf_InvalidatesPadesSignature_TC03()
    {
        PkiHierarchy pki = BuildTwoTierPki();
        IssuedIdentity partyA = IssueSignerCertificate(
            pki,
            commonName: "Nguyen Van An",
            email: "an.nguyen@ben-a.vn",
            organization: "Cong ty TNHH Giai Phap So Ben A");

        byte[] unsignedPdf = CreateSampleContractPdf();
        byte[] signedPdf = SignPdfPades(
            unsignedPdf,
            partyA,
            pki,
            fieldName: "Signature_PartyA",
            rect: new Rectangle(55, 130, 220, 85),
            signerLabel: "DAI DIEN BEN A\nKy boi: Nguyen Van An",
            reason: "Ky xac nhan gia tri hop dong",
            location: "TP. Ho Chi Minh, Viet Nam");

        // Sửa đúng 1 byte trong nội dung hợp đồng ("100,000,000 VND" -> "900,000,000 VND") mà không làm hỏng cấu trúc xref PDF.
        byte[] tamperedPdf = TamperSingleAmountByte(signedPdf);

        using PdfDocument pdfDoc = new(new PdfReader(new MemoryStream(tamperedPdf)));
        SignatureUtil sigUtil = new(pdfDoc);
        PdfPKCS7 pkcs7 = sigUtil.ReadSignatureData("Signature_PartyA");

        Assert.False(pkcs7.VerifySignatureIntegrityAndAuthenticity());
    }

    [Fact]
    public void UntrustedRootCa_FailsPkixChainVerification()
    {
        PkiHierarchy realPki = BuildTwoTierPki();
        PkiHierarchy roguePki = BuildTwoTierPki();

        IssuedIdentity rogueSigner = IssueSignerCertificate(
            roguePki,
            commonName: "Ke Gia Mao",
            email: "attacker@rogue.local",
            organization: "Rogue Org");

        Assert.ThrowsAny<Exception>(() =>
            ValidateCertificateChainToRoot(
                rogueSigner.Certificate,
                roguePki.IssuingCa.Certificate,
                realPki.RootCa.Certificate));
    }

    private static PkiHierarchy BuildTwoTierPki()
    {
        SecureRandom random = new();
        // Đặt khoảng hiệu lực rộng (-2 năm đến +8 năm) để chứng thư luôn hợp lệ dù đồng hồ máy kiểm tra ở năm 2025 hay 2026.
        DateTime notBefore = DateTime.UtcNow.AddYears(-2);

        AsymmetricCipherKeyPair rootKeys = GenerateRsaKeyPair(random);
        X509Name rootDn = new("C=VN, O=TrustSign Academic PKI, CN=TrustSign Root CA");
        X509V3CertificateGenerator rootGen = new();
        rootGen.SetSerialNumber(BigInteger.ProbablePrime(120, random));
        rootGen.SetIssuerDN(rootDn);
        rootGen.SetSubjectDN(rootDn);
        rootGen.SetNotBefore(notBefore);
        rootGen.SetNotAfter(DateTime.UtcNow.AddYears(8));
        rootGen.SetPublicKey(rootKeys.Public);
        rootGen.AddExtension(X509Extensions.BasicConstraints, critical: true, new BasicConstraints(1));
        rootGen.AddExtension(
            X509Extensions.KeyUsage,
            critical: true,
            new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));
        rootGen.AddExtension(
            X509Extensions.SubjectKeyIdentifier,
            critical: false,
            X509ExtensionUtilities.CreateSubjectKeyIdentifier(rootKeys.Public));

        X509Certificate rootCert = rootGen.Generate(new Asn1SignatureFactory("SHA256WITHRSA", rootKeys.Private, random));

        AsymmetricCipherKeyPair issuingKeys = GenerateRsaKeyPair(random);
        X509Name issuingDn = new("C=VN, O=TrustSign Academic PKI, CN=TrustSign Issuing CA v1");
        X509V3CertificateGenerator issuingGen = new();
        issuingGen.SetSerialNumber(BigInteger.ProbablePrime(120, random));
        issuingGen.SetIssuerDN(rootDn);
        issuingGen.SetSubjectDN(issuingDn);
        issuingGen.SetNotBefore(notBefore);
        issuingGen.SetNotAfter(DateTime.UtcNow.AddYears(5));
        issuingGen.SetPublicKey(issuingKeys.Public);
        issuingGen.AddExtension(X509Extensions.BasicConstraints, critical: true, new BasicConstraints(0));
        issuingGen.AddExtension(
            X509Extensions.KeyUsage,
            critical: true,
            new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));
        issuingGen.AddExtension(
            X509Extensions.SubjectKeyIdentifier,
            critical: false,
            X509ExtensionUtilities.CreateSubjectKeyIdentifier(issuingKeys.Public));
        issuingGen.AddExtension(
            X509Extensions.AuthorityKeyIdentifier,
            critical: false,
            X509ExtensionUtilities.CreateAuthorityKeyIdentifier(rootCert));

        X509Certificate issuingCert = issuingGen.Generate(new Asn1SignatureFactory("SHA256WITHRSA", rootKeys.Private, random));

        return new PkiHierarchy(
            new IssuedIdentity(rootCert, rootKeys.Private),
            new IssuedIdentity(issuingCert, issuingKeys.Private),
            random);
    }

    private static IssuedIdentity IssueSignerCertificate(
        PkiHierarchy pki,
        string commonName,
        string email,
        string organization)
    {
        AsymmetricCipherKeyPair signerKeys = GenerateRsaKeyPair(pki.Random);
        X509Name subjectDn = new($"C=VN, O={organization}, CN={commonName}, E={email}");

        X509V3CertificateGenerator certGen = new();
        certGen.SetSerialNumber(BigInteger.ProbablePrime(120, pki.Random));
        certGen.SetIssuerDN(pki.IssuingCa.Certificate.SubjectDN);
        certGen.SetSubjectDN(subjectDn);
        certGen.SetNotBefore(DateTime.UtcNow.AddYears(-2));
        certGen.SetNotAfter(DateTime.UtcNow.AddYears(2));
        certGen.SetPublicKey(signerKeys.Public);

        certGen.AddExtension(X509Extensions.BasicConstraints, critical: true, new BasicConstraints(false));
        certGen.AddExtension(
            X509Extensions.KeyUsage,
            critical: true,
            new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.NonRepudiation));
        certGen.AddExtension(
            X509Extensions.ExtendedKeyUsage,
            critical: false,
            new ExtendedKeyUsage(KeyPurposeID.id_kp_emailProtection));
        certGen.AddExtension(
            X509Extensions.SubjectKeyIdentifier,
            critical: false,
            X509ExtensionUtilities.CreateSubjectKeyIdentifier(signerKeys.Public));
        certGen.AddExtension(
            X509Extensions.AuthorityKeyIdentifier,
            critical: false,
            X509ExtensionUtilities.CreateAuthorityKeyIdentifier(pki.IssuingCa.Certificate));

        X509Certificate cert = certGen.Generate(
            new Asn1SignatureFactory("SHA256WITHRSA", pki.IssuingCa.PrivateKey, pki.Random));

        return new IssuedIdentity(cert, signerKeys.Private);
    }

    private static AsymmetricCipherKeyPair GenerateRsaKeyPair(SecureRandom random)
    {
        RsaKeyPairGenerator generator = new();
        generator.Init(new KeyGenerationParameters(random, 2048));
        return generator.GenerateKeyPair();
    }

    private static byte[] CreateSampleContractPdf()
    {
        using MemoryStream output = new();
        // Tắt nén luồng trang để minh họa trực quan kịch bản sửa 1 byte nội dung ("100,000,000 VND" -> "900,000,000 VND") trên Adobe Reader.
        WriterProperties writerProps = new WriterProperties().SetCompressionLevel(CompressionConstants.NO_COMPRESSION);
        using (PdfWriter writer = new(output, writerProps))
        using (PdfDocument pdf = new(writer))
        using (Document doc = new(pdf, PageSize.A4))
        {
            doc.SetMargins(50, 55, 50, 55);
            PdfFont unicodeFont = LoadUnicodeOrStandardFont();
            PdfFont asciiFont = PdfFontFactory.CreateFont();
            doc.SetFont(unicodeFont);

            doc.Add(new Paragraph("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(11)
                .SimulateBold());
            doc.Add(new Paragraph("Độc lập - Tự do - Hạnh phúc")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(10)
                .SetMarginBottom(18));

            doc.Add(new Paragraph("HỢP ĐỒNG DỊCH VỤ PHẦN MỀM HỢP ĐỒNG ĐIỆN TỬ CHỮ TÍN")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(14)
                .SimulateBold()
                .SetFontColor(new DeviceRgb(31, 95, 168)));
            doc.Add(new Paragraph("Số: HĐ-2026/CHU-TIN-001")
                .SetTextAlignment(TextAlignment.CENTER)
                .SetFontSize(10)
                .SetMarginBottom(18));

            doc.Add(new Paragraph("1. BÊN THUÊ DỊCH VỤ (BÊN A): Công ty TNHH Giải Pháp Số Bên A — Đại diện: Ông Nguyễn Văn An.").SetFontSize(11));
            doc.Add(new Paragraph("2. BÊN CUNG CẤP (BÊN B): Công ty Cổ Phần Thương Mại Bên B — Đại diện: Bà Trần Thị Bình.").SetFontSize(11));

            Paragraph amountLine = new Paragraph()
                .Add(new Text("3. TỔNG GIÁ TRỊ HỢP ĐỒNG: ").SetFont(unicodeFont).SimulateBold())
                .Add(new Text(OriginalAmountText).SetFont(asciiFont).SimulateBold())
                .Add(new Text(" (Đã bao gồm thuế GTGT).").SetFont(unicodeFont))
                .SetFontSize(11)
                .SetMarginTop(6);
            doc.Add(amountLine);

            doc.Add(new Paragraph(
                    "4. CAM KẾT KÝ SỐ PADES: Hai bên thống nhất ký kết hợp đồng điện tử bằng chữ ký số X.509 theo chuẩn ETSI PAdES (Incremental Update). Mọi sửa đổi dù chỉ 1 byte sau khi ký sẽ làm mất tính toàn vẹn của tài liệu.")
                .SetFontSize(10.5f)
                .SetMarginTop(6)
                .SetMarginBottom(30));

            Table signHeader = new Table(UnitValue.CreatePercentArray([50f, 50f])).UseAllAvailableWidth();
            signHeader.AddCell(new Cell().SetBorder(Border.NO_BORDER)
                .Add(new Paragraph("ĐẠI DIỆN BÊN A").SetTextAlignment(TextAlignment.CENTER).SimulateBold().SetFontSize(11)));
            signHeader.AddCell(new Cell().SetBorder(Border.NO_BORDER)
                .Add(new Paragraph("ĐẠI DIỆN BÊN B").SetTextAlignment(TextAlignment.CENTER).SimulateBold().SetFontSize(11)));
            doc.Add(signHeader);
        }

        return output.ToArray();
    }

    private static byte[] SignPdfPades(
        byte[] sourcePdf,
        IssuedIdentity signer,
        PkiHierarchy pki,
        string fieldName,
        Rectangle rect,
        string signerLabel,
        string reason,
        string location)
    {
        using MemoryStream input = new(sourcePdf);
        using MemoryStream output = new();

        PdfReader reader = new(input);
        StampingProperties stamping = new StampingProperties().UseAppendMode();

        SignatureFieldAppearance appearance = new SignatureFieldAppearance(fieldName)
            .SetContent(signerLabel);

        SignerProperties signerProps = new SignerProperties()
            .SetFieldName(fieldName)
            .SetPageNumber(1)
            .SetPageRect(rect)
            .SetReason(reason)
            .SetLocation(location)
            .SetSignatureCreator("Chu Tin (TrustSign) PAdES Engine")
            .SetSignatureAppearance(appearance);

        PdfSigner pdfSigner = new(reader, output, stamping);
        pdfSigner.SetSignerProperties(signerProps);

        IExternalSignature externalSignature = new PrivateKeySignature(
            new PrivateKeyBC(signer.PrivateKey),
            DigestAlgorithms.SHA256);

        IX509Certificate[] chain =
        [
            new X509CertificateBC(signer.Certificate),
            new X509CertificateBC(pki.IssuingCa.Certificate),
            new X509CertificateBC(pki.RootCa.Certificate)
        ];

        // CryptoStandard.CADES sinh chữ ký chuẩn PAdES-B-B (đính kèm thuộc tính ESSCertIDv2 bảo vệ chứng thư người ký).
        pdfSigner.SignDetached(
            externalSignature,
            chain,
            crlList: null,
            ocspClient: null,
            tsaClient: null,
            estimatedSize: 12_288,
            PdfSigner.CryptoStandard.CADES);

        return output.ToArray();
    }

    private static void ValidateCertificateChainToRoot(
        X509Certificate signerCert,
        X509Certificate issuingCaCert,
        X509Certificate trustedRootCert)
    {
        HashSet<TrustAnchor> trustAnchors = [new TrustAnchor(trustedRootCert, null)];
        X509CertStoreSelector targetConstraints = new() { Certificate = signerCert };

        PkixBuilderParameters builderParams = new(trustAnchors, targetConstraints)
        {
            IsRevocationEnabled = false,
            Date = DateTime.UtcNow
        };

        List<X509Certificate> intermediateCerts = [signerCert, issuingCaCert];
        builderParams.AddStoreCert(CollectionUtilities.CreateStore(intermediateCerts));

        PkixCertPathBuilder builder = new();
        PkixCertPathBuilderResult result = builder.Build(builderParams);

        Assert.Equal(trustedRootCert.SubjectDN, result.TrustAnchor.TrustedCert.SubjectDN);
    }

    private static X509Certificate UnwrapCertificate(IX509Certificate wrapped) =>
        ((X509CertificateBC)wrapped).GetCertificate();

    private static byte[] TamperSingleAmountByte(byte[] signedPdf)
    {
        byte[] copy = (byte[])signedPdf.Clone();
        byte[] needle = Encoding.ASCII.GetBytes(OriginalAmountText);
        int index = copy.AsSpan().IndexOf(needle);
        Assert.True(index >= 0, "Phải tìm thấy chuỗi giá trị hợp đồng trong luồng PDF.");

        // Đổi ký tự đầu tiên '1' (0x31) thành '9' (0x39): tổng độ dài file giữ nguyên 100%, chỉ khác đúng 1 byte nội dung.
        copy[index] = (byte)TamperedAmountText[0];
        return copy;
    }

    private static PdfFont LoadUnicodeOrStandardFont()
    {
        string windowsArial = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            "arial.ttf");

        if (File.Exists(windowsArial))
        {
            return PdfFontFactory.CreateFont(windowsArial, PdfEncodings.IDENTITY_H);
        }

        return PdfFontFactory.CreateFont();
    }

    private static void ExportArtifactsForManualReaderInspection(PkiHierarchy pki, byte[] validTwoSigPdf)
    {
        string? repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            return;
        }

        string outDir = System.IO.Path.Combine(repoRoot, "artifacts", "pades-spike");
        Directory.CreateDirectory(outDir);

        File.WriteAllBytes(System.IO.Path.Combine(outDir, "trustsign-root-ca.cer"), pki.RootCa.Certificate.GetEncoded());
        File.WriteAllBytes(System.IO.Path.Combine(outDir, "trustsign-issuing-ca.cer"), pki.IssuingCa.Certificate.GetEncoded());
        File.WriteAllBytes(System.IO.Path.Combine(outDir, "hop-dong-mau-2-chu-ky.pdf"), validTwoSigPdf);
        File.WriteAllBytes(
            System.IO.Path.Combine(outDir, "hop-dong-bi-sua-1-byte.pdf"),
            TamperSingleAmountByte(validTwoSigPdf));
    }

    private static string? FindRepoRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(System.IO.Path.Combine(current.FullName, "TrustSign.slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        return null;
    }

    private sealed record IssuedIdentity(X509Certificate Certificate, AsymmetricKeyParameter PrivateKey);

    private sealed record PkiHierarchy(
        IssuedIdentity RootCa,
        IssuedIdentity IssuingCa,
        SecureRandom Random);
}
