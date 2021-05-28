#if BIOSHOCK
    namespace UELib.Core
    {
        /// <summary>
        /// WeakReference Property
        /// </summary>
        [UnrealRegisterClass]
        public class UXWeakReferenceProperty : UObjectProperty
        {
            /// <inheritdoc/>
            public override string GetFriendlyPropType()
            {
                return base.GetFriendlyPropType() + "&";
            }
        }
    }
#endif