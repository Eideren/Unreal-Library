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
            else if (value.EndsWith('f') || double.TryParse(value, out _))
                type = "double";

            if( value.EndsWith( ".f" ) )
            {
                value = value.Remove( value.Length - 2, 1 );
            }


            return $"public const {type} {Name} = {value};";
        }
    }
}
#endif