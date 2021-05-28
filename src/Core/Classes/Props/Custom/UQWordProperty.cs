#if BIOSHOCK
    namespace UELib.Core
    {
        /// <summary>
        /// QWord Property
        /// </summary>
        [UnrealRegisterClass]
        public class UQWordProperty : UIntProperty
        {
            /// <inheritdoc/>
            public override string GetFriendlyPropType()
            {
                return "Qword";
            }
        }
    }
#endif