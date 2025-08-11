using UnityEngine;
using Sinkii09.Engine.Services;
using UnityEngine.UI;
public class MainMenuScreen : UIScreen
{
    [SerializeField] 
    private Image _backgroundImage;
    public override bool OnBackPressed()
    {
        return base.OnBackPressed();
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    protected override void OnShow()
    {
        base.OnShow();
    }
}
