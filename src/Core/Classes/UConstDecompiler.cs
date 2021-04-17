#if DECOMPILE
namespace UELib.Core
{
    public partial class UConst
    {
        /// <summary>
        /// Decompiles this object into a text format of:
        ///
        /// const NAME = VALUE;
        /// </summary>
        /// <returns></returns>
        public override string Decompile()
        {
            var value = Value.Trim();
            string type = "";
            if (value.StartsWith("\""))
                type = "string";
            else if (value.StartsWith("\'"))
            {
                type = "string";
                value = value.Replace("\'", "\"");
            }
            else if (int.TryParse(value, out _) || value.StartsWith("0x"))
                type = "int";
            else if (long.TryParse(value, out _))
                type = "long";
            else if (value.EndsWith('f') || float.TryParse(value, out _))
                type = "double";

            return "public const " + type + " " + Name + " = " + value + ";";
        }
    }
}
#endif