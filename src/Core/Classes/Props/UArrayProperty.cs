using UELib.Types;

namespace UELib.Core
{
    /// <summary>
    /// Dynamic Array Property
    /// </summary>
    [UnrealRegisterClass]
    public class UArrayProperty : UProperty
    {
        #region Serialized Members
        public UProperty InnerProperty;
        #endregion

        /// <summary>
        /// Creates a new instance of the UELib.Core.UArrayProperty class.
        /// </summary>
        public UArrayProperty()
        {
            Type = PropertyType.ArrayProperty;
        }

        protected override void Deserialize()
        {
            base.Deserialize();

            int innerIndex = _Buffer.ReadObjectIndex();
            InnerProperty = (UProperty)GetIndexObject( innerIndex );
        }

        /// <inheritdoc/>
        public override string GetFriendlyPropType()
        {
            if( InnerProperty != null )
            {
                return $"array<{GetFriendlyInnerType()}>";
            }
            return "array";
        }

        public override string GetFriendlyInnerType()
        {
            if (InnerProperty == null)
                return "@NULL";
            if (InnerProperty.IsClassType("ClassProperty") || InnerProperty.IsClassType("DelegateProperty"))
                return " " + InnerProperty.FormatFlags() + InnerProperty.GetFriendlyType() + " ";
            return InnerProperty.FormatFlags() + InnerProperty.GetFriendlyType();
        }
    }
}