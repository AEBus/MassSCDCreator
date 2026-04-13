using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MassSCDCreator.Localization;

public sealed partial class UiTextCatalog : ObservableObject {
    private ResourceDictionary? _cachedDictionary;

    public string this[string key] {
        get {
            var resources = GetDictionary();
            return resources.Contains( key ) ? resources[key] as string ?? $"!{key}!" : $"!{key}!";
        }
    }

    public string Format( string key, params object[] args ) =>
        string.Format( CultureInfo.CurrentCulture, this[key], args );

    private ResourceDictionary GetDictionary() {
        if( _cachedDictionary is not null ) {
            return _cachedDictionary;
        }

        _cachedDictionary = ( ResourceDictionary )Application.LoadComponent(
            new Uri( "/MassSCDCreator;component/Localization/Strings.en.xaml", UriKind.Relative ) );
        return _cachedDictionary;
    }
}
