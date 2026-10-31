using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// Parameter BehaviorsのCustom式を解析した結果
    ///
    /// 命令数の数え方はVRChatのドキュメントに合わせる。入力の読み取り、数値定数の読み込み、
    /// 演算子と関数の適用をそれぞれ1命令と数え、括弧とカンマは数えない。
    /// 定数だけの部分式は、アバター全体の上限に数える前に1つの定数へ簡約される
    /// （例: <c>JawOpen * (2 + 3)</c> は <c>JawOpen * 5</c> と同じ3命令）
    /// </summary>
    internal sealed class CustomExpressionInfo
    {
        public CustomExpressionInfo(
            string error,
            int tokenCount,
            int instructionsBeforeFolding,
            int instructions,
            int operations,
            IReadOnlyList<string> inputs)
        {
            Error = error;
            TokenCount = tokenCount;
            InstructionsBeforeFolding = instructionsBeforeFolding;
            Instructions = instructions;
            Operations = operations;
            Inputs = inputs;
        }

        /// <summary>構文か上限の誤り。正しい式ならnull</summary>
        public string Error { get; }

        public bool IsValid => Error == null;

        public int TokenCount { get; }

        /// <summary>定数の簡約前の命令数（1式あたり256まで）</summary>
        public int InstructionsBeforeFolding { get; }

        /// <summary>定数の簡約後の命令数（アバター全体で2048まで）</summary>
        public int Instructions { get; }

        /// <summary>Face Tracking Operationsに数えられる数。数値定数は数えない</summary>
        public int Operations { get; }

        /// <summary>式が読む入力の名前（重複なし、出てきた順）</summary>
        public IReadOnlyList<string> Inputs { get; }
    }

    /// <summary>
    /// Custom式の解析と命令数の計算
    ///
    /// 上限はVRChat SDK 3.10.6の<c>FaceTrackingExpressionValidator</c>に合わせる
    /// （3.10.6-beta.2から、長さ・トークン数・命令数・深さがそれぞれ2〜4倍に上がった）
    /// </summary>
    internal static class CustomExpression
    {
        /// <summary>アバター上のすべてのCustom式で共有する、簡約後の命令数の上限</summary>
        public const int AvatarInstructionLimit = 2048;

        public const int MaxLength = 1024;
        public const int MaxTokens = 256;
        public const int MaxInstructionsBeforeFolding = 256;

        /// <summary>括弧（関数呼び出しを含む）の入れ子の深さの上限</summary>
        public const int MaxNestingDepth = 32;

        /// <summary>評価に要るスタックの深さの上限</summary>
        public const int MaxStackDepth = 32;

        /// <summary>数値定数の桁数（小数点を含む文字数）の上限</summary>
        public const int MaxNumericLiteralLength = 20;

        /// <summary>数値定数の絶対値の上限</summary>
        public const double MaxNumericMagnitude = 10000;

        /// <summary>
        /// UEキー以外に使える入力（Voiceとビゼーム）。大文字と小文字も一致させる必要があり、
        /// ビゼームの名前はVRChatのドキュメントどおりすべて小文字で書く
        /// </summary>
        private static readonly string[] ExtraInputs =
        {
            "Voice",
            "Viseme_sil", "Viseme_pp", "Viseme_ff", "Viseme_th", "Viseme_dd",
            "Viseme_kk", "Viseme_ch", "Viseme_ss", "Viseme_nn", "Viseme_rr",
            "Viseme_aa", "Viseme_e", "Viseme_ih", "Viseme_oh", "Viseme_ou",
        };

        private static readonly Dictionary<string, int> Functions = new Dictionary<string, int>
        {
            { "abs", 1 },
            { "min", 2 },
            { "max", 2 },
            { "clamp", 3 },
        };

        private static HashSet<string> _inputNames;

        private static HashSet<string> InputNames =>
            _inputNames ?? (_inputNames = new HashSet<string>(FaceTrackingGroupTable.GetKeyNames().Concat(ExtraInputs)));

        public static bool IsKnownInput(string name)
        {
            return InputNames.Contains(name);
        }

        public static CustomExpressionInfo Analyze(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return Fail("empty expression", 0);
            }

            if (expression.Length > MaxLength)
            {
                return Fail($"longer than {MaxLength} characters", 0);
            }

            List<Token> tokens;
            try
            {
                tokens = Tokenize(expression);
            }
            catch (FormatException e)
            {
                return Fail(e.Message, 0);
            }

            if (tokens.Count > MaxTokens)
            {
                return Fail($"more than {MaxTokens} tokens", tokens.Count);
            }

            if (NestingDepth(tokens) > MaxNestingDepth)
            {
                return Fail($"nests more than {MaxNestingDepth} levels deep", tokens.Count);
            }

            Node root;
            try
            {
                var parser = new Parser(tokens);
                root = parser.ParseExpression();
                parser.ExpectEnd();
            }
            catch (FormatException e)
            {
                return Fail(e.Message, tokens.Count);
            }

            var unknown = root.Inputs().FirstOrDefault(name => !IsKnownInput(name));
            if (unknown != null)
            {
                return Fail($"unknown input '{unknown}'", tokens.Count);
            }

            int before = root.Count();
            if (before > MaxInstructionsBeforeFolding)
            {
                return Fail($"more than {MaxInstructionsBeforeFolding} instructions", tokens.Count);
            }

            if (root.StackDepth() > MaxStackDepth)
            {
                return Fail($"needs an evaluation stack deeper than {MaxStackDepth}", tokens.Count);
            }

            var folded = root.Fold();
            return new CustomExpressionInfo(
                null,
                tokens.Count,
                before,
                folded.Count(),
                folded.CountOperations(),
                root.Inputs().Distinct().ToArray());
        }

        private static int NestingDepth(List<Token> tokens)
        {
            int depth = 0;
            int max = 0;
            foreach (var token in tokens)
            {
                if (token.Kind == TokenKind.OpenParen)
                {
                    max = Math.Max(max, ++depth);
                }
                else if (token.Kind == TokenKind.CloseParen)
                {
                    depth--;
                }
            }

            return max;
        }

        private static CustomExpressionInfo Fail(string error, int tokenCount)
        {
            return new CustomExpressionInfo(error, tokenCount, 0, 0, 0, new string[0]);
        }

        private enum TokenKind
        {
            Number,
            Identifier,
            Operator,
            OpenParen,
            CloseParen,
            Comma,
        }

        private readonly struct Token
        {
            public Token(TokenKind kind, string text)
            {
                Kind = kind;
                Text = text;
            }

            public TokenKind Kind { get; }

            public string Text { get; }
        }

        private static List<Token> Tokenize(string text)
        {
            var tokens = new List<Token>();
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (char.IsDigit(c) || c == '.')
                {
                    int start = i;
                    while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
                    {
                        i++;
                    }

                    tokens.Add(new Token(TokenKind.Number, text.Substring(start, i - start)));
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                    {
                        i++;
                    }

                    tokens.Add(new Token(TokenKind.Identifier, text.Substring(start, i - start)));
                    continue;
                }

                switch (c)
                {
                    case '+':
                    case '-':
                    case '*':
                    case '/':
                        tokens.Add(new Token(TokenKind.Operator, c.ToString()));
                        break;
                    case '(':
                        tokens.Add(new Token(TokenKind.OpenParen, "("));
                        break;
                    case ')':
                        tokens.Add(new Token(TokenKind.CloseParen, ")"));
                        break;
                    case ',':
                        tokens.Add(new Token(TokenKind.Comma, ","));
                        break;
                    default:
                        throw new FormatException($"unexpected character '{c}'");
                }

                i++;
            }

            return tokens;
        }

        /// <summary>
        /// 加減算 &lt; 乗除算 &lt; 単項マイナス &lt; 数値・入力・関数・括弧 の再帰下降構文解析
        /// </summary>
        private sealed class Parser
        {
            private readonly List<Token> _tokens;
            private int _position;

            public Parser(List<Token> tokens)
            {
                _tokens = tokens;
            }

            public Node ParseExpression()
            {
                var left = ParseTerm();
                while (PeekOperator("+") || PeekOperator("-"))
                {
                    var op = _tokens[_position++].Text;
                    left = new Node(op, left, ParseTerm());
                }

                return left;
            }

            public void ExpectEnd()
            {
                if (_position < _tokens.Count)
                {
                    throw new FormatException($"unexpected '{_tokens[_position].Text}'");
                }
            }

            private Node ParseTerm()
            {
                var left = ParseUnary();
                while (PeekOperator("*") || PeekOperator("/"))
                {
                    var op = _tokens[_position++].Text;
                    left = new Node(op, left, ParseUnary());
                }

                return left;
            }

            private Node ParseUnary()
            {
                if (PeekOperator("-"))
                {
                    _position++;
                    return new Node("neg", ParseUnary());
                }

                return ParsePrimary();
            }

            private Node ParsePrimary()
            {
                if (_position >= _tokens.Count)
                {
                    throw new FormatException("unexpected end of expression");
                }

                var token = _tokens[_position++];
                switch (token.Kind)
                {
                    case TokenKind.Number:
                        if (!double.TryParse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                        {
                            throw new FormatException($"invalid number '{token.Text}'");
                        }

                        if (token.Text.Length > MaxNumericLiteralLength || Math.Abs(value) > MaxNumericMagnitude)
                        {
                            throw new FormatException($"number '{token.Text}' is out of range");
                        }

                        return Node.Constant(value);
                    case TokenKind.Identifier:
                        if (_position < _tokens.Count && _tokens[_position].Kind == TokenKind.OpenParen)
                        {
                            return ParseCall(token.Text);
                        }

                        return Node.Input(token.Text);
                    case TokenKind.OpenParen:
                        var inner = ParseExpression();
                        Expect(TokenKind.CloseParen);
                        return inner;
                    default:
                        throw new FormatException($"unexpected '{token.Text}'");
                }
            }

            private Node ParseCall(string name)
            {
                if (!Functions.TryGetValue(name, out var arity))
                {
                    throw new FormatException($"unknown function '{name}'");
                }

                Expect(TokenKind.OpenParen);
                var args = new List<Node> { ParseExpression() };
                while (_position < _tokens.Count && _tokens[_position].Kind == TokenKind.Comma)
                {
                    _position++;
                    args.Add(ParseExpression());
                }

                Expect(TokenKind.CloseParen);
                if (args.Count != arity)
                {
                    throw new FormatException($"{name} takes {arity} arguments");
                }

                return new Node(name, args.ToArray());
            }

            private bool PeekOperator(string op)
            {
                return _position < _tokens.Count
                       && _tokens[_position].Kind == TokenKind.Operator
                       && _tokens[_position].Text == op;
            }

            private void Expect(TokenKind kind)
            {
                if (_position >= _tokens.Count || _tokens[_position].Kind != kind)
                {
                    throw new FormatException("missing " + (kind == TokenKind.CloseParen ? "')'" : "'('"));
                }

                _position++;
            }
        }

        private sealed class Node
        {
            private readonly Node[] _children;
            private readonly double _value;

            public Node(string op, params Node[] children)
            {
                Op = op;
                _children = children;
            }

            private Node(string op, double value, string name)
            {
                Op = op;
                _value = value;
                Name = name;
                _children = new Node[0];
            }

            public string Op { get; }

            public string Name { get; }

            public static Node Constant(double value)
            {
                return new Node("const", value, null);
            }

            public static Node Input(string name)
            {
                return new Node("input", 0, name);
            }

            private bool IsConstant => Op == "const";

            /// <summary>命令数。葉も演算も1つずつ数える</summary>
            public int Count()
            {
                return 1 + _children.Sum(child => child.Count());
            }

            /// <summary>
            /// 左から順に評価したときに積まれるスタックの最大の深さ。
            /// 二項演算子は右辺を左辺の上に、関数は引数を順に積む
            /// </summary>
            public int StackDepth()
            {
                int max = 1;
                for (int i = 0; i < _children.Length; i++)
                {
                    max = Math.Max(max, i + _children[i].StackDepth());
                }

                return max;
            }

            /// <summary>Face Tracking Operationsの数（数値定数を除く）</summary>
            public int CountOperations()
            {
                return (IsConstant ? 0 : 1) + _children.Sum(child => child.CountOperations());
            }

            public IEnumerable<string> Inputs()
            {
                if (Op == "input")
                {
                    yield return Name;
                }

                foreach (var name in _children.SelectMany(child => child.Inputs()))
                {
                    yield return name;
                }
            }

            /// <summary>
            /// 定数だけの部分式を1つの定数にまとめる。値の計算は命令数に影響しないので0で置く
            /// </summary>
            public Node Fold()
            {
                if (_children.Length == 0)
                {
                    return this;
                }

                var folded = _children.Select(child => child.Fold()).ToArray();
                return folded.All(child => child.IsConstant) ? Constant(0) : new Node(Op, folded);
            }
        }
    }
}
