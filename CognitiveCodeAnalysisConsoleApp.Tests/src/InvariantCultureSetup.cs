/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;

// Namespace-less on purpose: applies to every test in the assembly.
// Console output still depends on the current culture in production (known defect); tests pin it.
[SetUpFixture]
public class InvariantCultureSetup
{
    private CultureInfo? _culture;
    private CultureInfo? _uiCulture;
    private CultureInfo? _threadCulture;
    private CultureInfo? _threadUiCulture;

    [OneTimeSetUp]
    public void PinCulture()
    {
        _culture = CultureInfo.DefaultThreadCurrentCulture;
        _uiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        _threadCulture = CultureInfo.CurrentCulture;
        _threadUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    [OneTimeTearDown]
    public void RestoreCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _uiCulture;
        CultureInfo.CurrentCulture = _threadCulture!;
        CultureInfo.CurrentUICulture = _threadUiCulture!;
    }
}
