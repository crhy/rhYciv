using Model;
using Model.Controls;
using Model.Controls.Civilopedia;
using Moq;
using RhyCiv.UI.Classic;

namespace RhyCiv.Tests.Civilopedia;

/// <summary>
/// #106: the civilopedia used to offer a "Description" button on every page.
/// On a page that already shows its description the button only navigated to
/// what was on screen, and on Terrain pages that trip landed on the blurry
/// individual icon page. The fix (#104, #106) drops the button from every view:
/// <see cref="ClassicInterface.GetCivilopediaProperties"/> offers it nowhere.
/// These pin that, so a "Description" button creeping back into any
/// window-type/info-type combination fails the suite.
/// </summary>
public class CivilopediaButtonsTests
{
    private static ClassicInterface ShippedDefaults() =>
        new Mock<ClassicInterface>(new Mock<IMain>().Object).Object;

    public static TheoryData<CivilopediaWindowType, CivilopediaInfoType> EveryPage()
    {
        var pages = new TheoryData<CivilopediaWindowType, CivilopediaInfoType>();
        foreach (var window in Enum.GetValues<CivilopediaWindowType>())
        {
            foreach (var info in Enum.GetValues<CivilopediaInfoType>())
            {
                pages.Add(window, info);
            }
        }

        return pages;
    }

    [Theory]
    [MemberData(nameof(EveryPage))]
    public void NoPageOffersADescriptionButton(CivilopediaWindowType windowType, CivilopediaInfoType infoType)
    {
        var buttons = ShippedDefaults().GetCivilopediaProperties(
            new CivilopediaEntry(infoType, windowType)).Buttons;

        Assert.DoesNotContain("Description", buttons);
    }

    [Theory]
    [InlineData(CivilopediaInfoType.Improvements)]
    [InlineData(CivilopediaInfoType.Wonders)]
    [InlineData(CivilopediaInfoType.Units)]
    [InlineData(CivilopediaInfoType.Governments)]
    [InlineData(CivilopediaInfoType.Terrains)]
    [InlineData(CivilopediaInfoType.Concepts)]
    public void PlainInfoPageOffersExactlyGoBackAndClose(CivilopediaInfoType infoType)
    {
        var buttons = ShippedDefaults().GetCivilopediaProperties(
            new CivilopediaEntry(infoType, CivilopediaWindowType.Info)).Buttons;

        Assert.Equal(["Go Back", "Close"], buttons);
    }
}
