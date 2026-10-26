using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MFramework
{
    // 固定头部与尾部摘要使整个信封（包括修订号和版本）一并受校验保护。
    // 摘要仅检测意外损坏，不是签名，也不能防止玩家自行修改后重新计算摘要。
    internal sealed class MSaveEnvelope
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("MSFRAME1");
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        internal const int MaximumContentVersionBytes = 1024;

        internal MSaveEnvelope(MSaveMetadata metadata, byte[] payload)
        {
            Metadata = metadata;
            Payload = payload;
        }

        internal MSaveMetadata Metadata { get; }
        internal byte[] Payload { get; }

        internal byte[] Encode()
        {
            byte[] content = Utf8.GetBytes(Metadata.ContentVersion);
            using (var buffer = new MemoryStream())
            {
                using (var writer = new BinaryWriter(buffer, Utf8, true))
                {
                    writer.Write(Magic);
                    writer.Write(1); // 信封协议版本；与应用数据结构版本分开演进。
                    writer.Write(Metadata.SchemaVersion);
                    writer.Write(content.Length);
                    writer.Write(Metadata.Revision);
                    writer.Write(Payload.Length);
                    writer.Write(Metadata.CommitId.ToByteArray());
                    writer.Write(content);
                    writer.Write(Payload);
                }
                byte[] body = buffer.ToArray();
                byte[] hash = Fingerprint(body);
                buffer.Write(hash, 0, hash.Length);
                return buffer.ToArray();
            }
        }

        internal static MSaveEnvelope Decode(byte[] snapshot)
        {
            if (snapshot.Length < 80)
                throw new MSaveFormatException(MSaveStatus.Corrupt, "快照长度不足。");
            byte[] digest;
            using (var sha = SHA256.Create())
                digest = sha.ComputeHash(snapshot, 0, snapshot.Length - 32);
            for (int i = 0; i < digest.Length; i++)
                if (digest[i] != snapshot[snapshot.Length - 32 + i])
                    throw new MSaveFormatException(MSaveStatus.Corrupt, "完整性摘要不匹配，内容或元数据已损坏。");

            using (var reader = new BinaryReader(new MemoryStream(snapshot, false), Utf8))
            {
                if (!Equal(reader.ReadBytes(Magic.Length), Magic))
                    throw new MSaveFormatException(MSaveStatus.Corrupt, "无法识别快照标识。");
                int formatVersion = reader.ReadInt32();
                if (formatVersion != 1)
                    throw new MSaveFormatException(MSaveStatus.UnsupportedVersion, "不支持此信封协议版本。");
                int schemaVersion = reader.ReadInt32();
                int contentLength = reader.ReadInt32();
                long revision = reader.ReadInt64();
                int payloadLength = reader.ReadInt32();
                var commitId = new Guid(reader.ReadBytes(16));
                if (schemaVersion < 1 || revision < 1 || commitId == Guid.Empty || contentLength < 0 ||
                    contentLength > MaximumContentVersionBytes || payloadLength < 0 ||
                    (long)contentLength + payloadLength + 80 != snapshot.Length)
                    throw new MSaveFormatException(MSaveStatus.Corrupt, "快照版本、修订号或长度字段无效。");
                string contentVersion;
                try { contentVersion = Utf8.GetString(reader.ReadBytes(contentLength)); }
                catch (DecoderFallbackException exception)
                {
                    throw new MSaveFormatException(MSaveStatus.Corrupt, "内容版本不是有效的 UTF-8。", exception);
                }
                return new MSaveEnvelope(new MSaveMetadata(schemaVersion, contentVersion, revision, commitId),
                    reader.ReadBytes(payloadLength));
            }
        }

        internal static byte[] Fingerprint(byte[] bytes)
        {
            if (bytes == null) return null; // 缺失本身也是恢复凭据的一部分。
            using (var sha = SHA256.Create()) return sha.ComputeHash(bytes);
        }

        internal static bool Equal(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }
    }

    internal sealed class MSaveFormatException : Exception
    {
        internal MSaveFormatException(MSaveStatus status, string message, Exception inner = null)
            : base(message, inner) { Status = status; }
        internal MSaveStatus Status { get; }
    }

    internal static class MSaveSlot
    {
        internal static void Validate(string slot)
        {
            if (string.IsNullOrEmpty(slot) || slot.Length > 64)
                throw new ArgumentException("槽名须为 1 至 64 个 ASCII 字母、数字、下划线或短横线。", nameof(slot));
            foreach (char character in slot)
                if (!(character >= 'a' && character <= 'z') && !(character >= 'A' && character <= 'Z') &&
                    !(character >= '0' && character <= '9') && character != '_' && character != '-')
                    throw new ArgumentException("槽名不能包含路径、空格、点号或其他特殊字符。", nameof(slot));
        }
    }
}
