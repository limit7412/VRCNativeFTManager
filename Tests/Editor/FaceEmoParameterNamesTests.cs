using System.Collections.Generic;
using NUnit.Framework;
using NativeFTManager.Domain;

namespace NativeFTManager.Tests
{
    /// <summary>
    /// FaceEmoのパラメータ名の解決を検証する
    /// </summary>
    public class FaceEmoParameterNamesTests
    {
        private const string Prefix = "FaceEmo_";

        private static IReadOnlyDictionary<string, string> FaceEmoRemap(string prefix)
        {
            return new Dictionary<string, string>
            {
                { FaceEmoParameterNames.RawEmoteLockEnable, prefix + FaceEmoParameterNames.RawEmoteLockEnable },
                { FaceEmoParameterNames.RawForceBlinkDisable, prefix + FaceEmoParameterNames.RawForceBlinkDisable },
                { FaceEmoParameterNames.RawEmote, prefix + FaceEmoParameterNames.RawEmote },
                { "CN_BYPASS", prefix + "CN_BYPASS" },
            };
        }

        [Test]
        public void Resolve_ReadsRenamedNames()
        {
            var names = FaceEmoParameterNames.Resolve(new[] { FaceEmoRemap(Prefix) });

            Assert.That(names.Resolved, Is.True);
            Assert.That(names.EmoteLockEnable, Is.EqualTo(Prefix + FaceEmoParameterNames.RawEmoteLockEnable));
            Assert.That(names.ForceBlinkDisable, Is.EqualTo(Prefix + FaceEmoParameterNames.RawForceBlinkDisable));
            Assert.That(names.Emote, Is.EqualTo(Prefix + FaceEmoParameterNames.RawEmote));
        }

        [Test]
        public void Resolve_KeepsNames_WhenNotRenamed()
        {
            var names = FaceEmoParameterNames.Resolve(new[] { FaceEmoRemap(string.Empty) });

            Assert.That(names.Resolved, Is.True);
            Assert.That(names.EmoteLockEnable, Is.EqualTo(FaceEmoParameterNames.RawEmoteLockEnable));
            Assert.That(names.Emote, Is.EqualTo(FaceEmoParameterNames.RawEmote));
        }

        /// <summary>
        /// FaceEmoは制御パラメータを1つのMA Parametersへまとめて登録する。
        /// 一部だけを持つコンポーネントは別のツールのものであり、そこから名前を引いてはいけない
        /// </summary>
        [Test]
        public void Resolve_IgnoresComponentsWithOnlySomeOfTheNames()
        {
            var partial = new Dictionary<string, string>
            {
                { FaceEmoParameterNames.RawEmote, "Other_" + FaceEmoParameterNames.RawEmote },
            };

            var names = FaceEmoParameterNames.Resolve(
                new IReadOnlyDictionary<string, string>[] { partial, FaceEmoRemap(Prefix) });

            Assert.That(names.Emote, Is.EqualTo(Prefix + FaceEmoParameterNames.RawEmote));
        }

        [Test]
        public void Resolve_FallsBackToRawNames_WhenNothingMatches()
        {
            var unrelated = new Dictionary<string, string> { { "Unrelated", "Unrelated" } };

            var names = FaceEmoParameterNames.Resolve(
                new IReadOnlyDictionary<string, string>[] { unrelated });

            Assert.That(names.Resolved, Is.False);
            Assert.That(names.EmoteLockEnable, Is.EqualTo(FaceEmoParameterNames.RawEmoteLockEnable));
            Assert.That(names.ForceBlinkDisable, Is.EqualTo(FaceEmoParameterNames.RawForceBlinkDisable));
            Assert.That(names.Emote, Is.EqualTo(FaceEmoParameterNames.RawEmote));
        }

        [Test]
        public void Resolve_FallsBackToRawNames_WhenNull()
        {
            var names = FaceEmoParameterNames.Resolve(null);

            Assert.That(names.Resolved, Is.False);
            Assert.That(names.Emote, Is.EqualTo(FaceEmoParameterNames.RawEmote));
        }
    }
}
