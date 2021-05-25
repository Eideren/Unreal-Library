using System.Collections.Generic;

namespace UELib.Core
{
    /// <summary>
    /// Represents a unreal enum.
    /// </summary>
    [UnrealRegisterClass]
    public partial class UEnum : UField
    {
        #region Serialized Members
        /// <summary>
        /// Names of each element in the UEnum.
        /// </summary>
        public IList<UName> Names;
        #endregion

        #region Constructors
        protected override void Deserialize()
        {
            base.Deserialize();

            int count = ReadCount();
            Names = new List<UName>(count);
            for( int i = 0; i < count; ++ i )
            {
                Names.Add( _Buffer.ReadNameReference() );
            }
        }
        #endregion

        public string ParseAsEnum(string str)
        {
            if (int.TryParse(str, out int index))
            {
                return ParseAsEnum(index);                
            }
            return $"(({GetFriendlyType()}){str})";
        }

        public string ParseAsEnum(int index, bool includeIndexComment = true)
        {
            if (index < Names.Count)
                return $"{GetFriendlyType()}.{Names[index]}{(includeIndexComment ? $"/*{index}*/" : "" )}";
            return $"/*val out of enum range*/(({GetFriendlyType()}){index})"; 
        }
    }
}