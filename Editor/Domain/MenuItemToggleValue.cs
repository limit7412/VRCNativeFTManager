using System;
using System.Collections.Generic;

namespace NativeFTManager.Domain
{
    /// <summary>
    /// MA Menu Itemのトグル値と型を、MAがビルド時に決めるのと同じ規則で求める
    ///
    /// 本製品はMAより前に動くため、MAが割り当てる値をまだ読めない。
    /// 同じ規則をここでなぞり、MAの割り当てと一致する値を先に求める。
    /// 規則はModular Avatar 1.10のParameterAssignerPassに合わせている。FE-JsT Bridgeから移したもの
    /// </summary>
    internal static class MenuItemToggleValue
    {
        /// <summary>
        /// トグル値からパラメーターの型を決める
        /// 0と1はBool、2以上の整数はInt、負の値と端数を持つ値はFloatになる
        /// </summary>
        public static SuppressionParameterType TypeOf(float value)
        {
            if (value < 0f || Math.Abs(value - Math.Round(value)) > 0.01f)
            {
                return SuppressionParameterType.Float;
            }

            return value > 1f ? SuppressionParameterType.Int : SuppressionParameterType.Bool;
        }

        /// <summary>
        /// 同じパラメーターを使うメニューアイテムすべてのトグル値から、パラメーターの型を決める
        /// MAは全項目の値を見て型を決めるため、1つでもFloatならFloat、次にInt、残りはBoolになる
        /// </summary>
        public static SuppressionParameterType TypeOf(IEnumerable<float> values)
        {
            var type = SuppressionParameterType.Bool;

            foreach (var value in values)
            {
                var current = TypeOf(value);
                if (current == SuppressionParameterType.Float)
                {
                    return SuppressionParameterType.Float;
                }

                if (current == SuppressionParameterType.Int)
                {
                    type = SuppressionParameterType.Int;
                }
            }

            return type;
        }

        /// <summary>
        /// 値の自動割り当て (automaticValue) が有効なメニューアイテムの値を求める
        /// </summary>
        /// <param name="isDefault">メニューアイテムの「既定値にする」設定</param>
        /// <param name="itemCount">同じパラメーターを参照するメニューアイテムの数 (自分を含む)</param>
        /// <param name="declaredDefault">
        /// Expression ParametersかMA Parametersで宣言済みのときの既定値。未宣言ならnull
        /// </param>
        /// <returns>求まった値。求められない場合はnull</returns>
        /// <remarks>
        /// 同じパラメーターを複数のメニューアイテムが参照する場合、MAは階層順に値を振っていく。
        /// その順序はビルド時のヒエラルキーで決まり、なぞると不一致を起こしやすいため扱わない。
        /// 単独のトグルならMAの割り当ては次のように決まる。
        /// 未宣言なら、既定値かどうかにかかわらず1になる。
        /// 宣言済みで既定値にするなら、宣言の既定値になる。
        /// 宣言済みで既定値にしないなら、宣言の既定値を避けて1から探した値になる
        /// </remarks>
        public static float? ResolveAutomatic(bool isDefault, int itemCount, float? declaredDefault)
        {
            if (itemCount != 1)
            {
                return null;
            }

            if (!declaredDefault.HasValue)
            {
                return 1f;
            }

            // MAは宣言の既定値を整数へ切り捨ててから使う
            var declared = (int)declaredDefault.Value;

            if (isDefault)
            {
                return declared;
            }

            return declared == 1 ? 2f : 1f;
        }
    }
}
