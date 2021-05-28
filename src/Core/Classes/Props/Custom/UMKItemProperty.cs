#if MKKE
    namespace UELib.Core
    {
        /// <summary>
        /// MK Item Property
        /// </summary>
        [UnrealRegisterClass]
        public class UMKItemProperty : UProperty
        {
            /// <inheritdoc/>
            public override string GetFriendlyPropType()
            {
                return "MKItem";
            }
        }
    }
#endif