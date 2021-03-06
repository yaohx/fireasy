﻿// -----------------------------------------------------------------------
// <copyright company="Fireasy"
//      email="faib920@126.com"
//      qq="55570729">
//   (c) Copyright Fireasy. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Fireasy.Data.Syntax
{
    public class OleDbMathSyntax : MathSyntax
    {
        /// <summary>
        /// 两个表达式进行与运算。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <param name="otherExp">参与运算的表达式。</param>
        /// <returns></returns>
        public override string And(object sourceExp, object otherExp)
        {
            return string.Format("({0} & {1})", sourceExp, otherExp);
        }

        /// <summary>
        /// 两个表达式进行或运算。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <param name="otherExp">参与运算的表达式。</param>
        /// <returns></returns>
        public override string Or(object sourceExp, object otherExp)
        {
            return string.Format("({0} | {1})", sourceExp, otherExp);
        }

        /// <summary>
        /// 对源表达式进行求余运算。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <param name="otherExp">参与运算的表达式。</param>
        /// <returns></returns>
        public override string Modulo(object sourceExp, object otherExp)
        {
            return string.Format("({0} MOD {1})", sourceExp, otherExp);
        }

        /// <summary>
        /// 对两个表达式进行异或运算。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <param name="otherExp">参与运算的表达式。</param>
        /// <returns></returns>
        public override string ExclusiveOr(object sourceExp, object otherExp)
        {
            return string.Format("({0} XOR {1})", sourceExp, otherExp);
        }

        /// <summary>
        /// 返回源表达式的最小整数值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Ceiling(object sourceExp)
        {
            return string.Format("INT({0})", sourceExp);
        }

        /// <summary>
        /// 将源表达式的小数位舍入。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <param name="digitExp">小数位数。</param>
        /// <returns></returns>
        public override string Round(object sourceExp, object digitExp = null)
        {
            return string.Format("ROUND({0}, {1})", sourceExp, digitExp ?? "0");
        }

        /// <summary>
        /// 返回源表达式的整数部份。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Truncate(object sourceExp)
        {
            return string.Format("ROUND({0}, 0)", sourceExp);
        }

        /// <summary>
        /// 返回源表达式的最大整数值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Floor(object sourceExp)
        {
            return string.Format("FIX({0})", sourceExp);
        }

        /// <summary>
        /// 返回以e为底的对数值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Log(object sourceExp)
        {
            return string.Format("LOG({0})", sourceExp);
        }

        /// <summary>
        /// 返回以10为底的对数值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Log10(object sourceExp)
        {
            return string.Format("LOG10({0})", sourceExp);
        }

        /// <summary>
        /// 返回e的指定次冪。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Exp(object sourceExp)
        {
            return string.Format("EXP({0})", sourceExp);
        }

        /// <summary>
        /// 返回源表达式的绝对值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Abs(object sourceExp)
        {
            return string.Format("ABS({0})", sourceExp);
        }

        /// <summary>
        /// 返回源表达式的反值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Negate(object sourceExp)
        {
            return string.Format("~{0}", sourceExp);
        }

        /// <summary>
        /// 返回源表达式的指定冪。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <param name="powerExp">冪。</param>
        /// <returns></returns>
        public override string Power(object sourceExp, object powerExp)
        {
            return string.Format("POWER({0}, {1})", sourceExp, powerExp);
        }

        /// <summary>
        /// 返回源表达式的二次开方值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Sqrt(object sourceExp)
        {
            return string.Format("SQRT({0})", sourceExp);
        }

        /// <summary>
        /// 返回源表达式的正弦值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Sin(object sourceExp)
        {
            return string.Format("SIN({0})", sourceExp);
        }

        /// <summary>
        /// 返回源表达式的余弦值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Cos(object sourceExp)
        {
            return string.Format("COS({0})", sourceExp);
        }

        /// <summary>
        /// 返回源表达式的正切值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Tan(object sourceExp)
        {
            return string.Format("TAN({0})", sourceExp);
        }

        /// <summary>
        /// 返回源表达式的反正弦值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Asin(object sourceExp)
        {
            return string.Format("ASIN({0})", sourceExp);
        }

        /// <summary>
        /// 返回源表达式的反余弦值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Acos(object sourceExp)
        {
            return string.Format("ACOS({0})", sourceExp);
        }

        /// <summary>
        /// 返回源表达式的反正切值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Atan(object sourceExp)
        {
            return string.Format("ATAN({0})", sourceExp);
        }

        /// <summary>
        /// 返回源表达式的符号。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <returns></returns>
        public override string Sign(object sourceExp)
        {
            return string.Format("SIGN({0})", sourceExp);
        }

        /// <summary>
        /// 返回源表达式左移后的值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <param name="shiftExp">位数。</param>
        /// <returns></returns>
        public override string LeftShift(object sourceExp, object shiftExp)
        {
            return string.Format("{0} * POWER(2, {1})", sourceExp, shiftExp);
        }

        /// <summary>
        /// 返回源表达式右移后的值。
        /// </summary>
        /// <param name="sourceExp">源表达式。</param>
        /// <param name="shiftExp">位数。</param>
        /// <returns></returns>
        public override string RightShift(object sourceExp, object shiftExp)
        {
            return string.Format("{0} / POWER(2, {1})", sourceExp, shiftExp);
        }
    }
}
