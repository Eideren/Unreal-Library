using System;

namespace UELib
{
    using System.Collections.Generic;
    using Core;



    public static class UDecompilingState
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Usage", "CA2211:NonConstantFieldsShouldNotBeVisible" )]
        public static string Tabs = String.Empty;

        public static void AddTabs( int count )
        {
            for( int i = 0; i < count; ++ i )
            {
                Tabs += UnrealConfig.Indention;
            }
        }

        public static void AddTab()
        {
            Tabs += UnrealConfig.Indention;
        }

        public static TabScopeDisposable TabScope()
        {
            AddTab();
            return new TabScopeDisposable();
        }

        public struct TabScopeDisposable : IDisposable
        {
            public void Dispose()
            {
                RemoveTab();
            }
        }

        public static void RemoveTabs( int count )
        {
            count *= UnrealConfig.Indention.Length;
            Tabs = count > Tabs.Length ? String.Empty : Tabs.Substring( 0, (Tabs.Length) - count );
        }

        public static void RemoveTab()
        {
            // TODO: FIXME! This should not occur but it does in MutBestTimes.KeyConsumed(huge nested switch cases)
            if( Tabs.Length == 0 )
                return;

            Tabs = Tabs.Substring( 0, Tabs.Length - UnrealConfig.Indention.Length );
        }

        public static void RemoveSpaces( int count )
        {
            if( Tabs.Length < count )
            {
                Tabs = String.Empty;
                return;
            }
            Tabs = Tabs.Substring( 0, Tabs.Length - count );
        }

        public static void ResetTabs()
        {
            Tabs = String.Empty;
        }
    }
}