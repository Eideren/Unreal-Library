﻿#if MKKE
    namespace UELib.Core
    {
        /// <summary>
        /// MK Item NoDestroy Property
        /// </summary>
        [UnrealRegisterClass]
        public class UMKItemNoDestroyProperty : UMKItemProperty
        {
            /// <inheritdoc/>
            public override string GetFriendlyPropType()
            {
                return "MKNoDestroyItem";
            }
        }
    }
#endif