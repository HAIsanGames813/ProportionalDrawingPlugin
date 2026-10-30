using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace ProportionalDrawingPlugin;

public enum ScaleReferenceDirection
{
    [Display(Name = nameof(Texts.Height_reference), ResourceType = typeof(Texts))]
    縦幅基準,

    [Display(Name = nameof(Texts.Width_reference), ResourceType = typeof(Texts))]
    横幅基準,
}

[VideoEffect(nameof(Texts.Proportional_scale), [VideoEffectCategories.Drawing], [], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class ProportionalScaleEffect : VideoEffectBase
{
    private const double ParameterLimit = 1_000_000_000d;
    private ScaleReferenceDirection direction;
    private bool useSeparateAxes;

    public override string Label => $"{Texts.Proportional_scale} [{Overall.GetValue(0, 1, 30):0.#}%, {Vertical.GetValue(0, 1, 30):0.#}%, {Horizontal.GetValue(0, 1, 30):0.#}%]";

    [Display(GroupName = nameof(Texts.Scale_tab), Name = nameof(Texts.Overall), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", -100, 100)]
    public Animation Overall { get; } = new(100, -ParameterLimit, ParameterLimit);

    [Display(GroupName = nameof(Texts.Scale_tab), Name = nameof(Texts.Reference_direction), ResourceType = typeof(Texts))]
    [EnumComboBox]
    public ScaleReferenceDirection Direction
    {
        get => direction;
        set => Set(ref direction, value);
    }

    [Display(GroupName = nameof(Texts.Scale_tab), Name = nameof(Texts.Use_separate_axes), ResourceType = typeof(Texts))]
    [ToggleSlider]
    public bool UseSeparateAxes
    {
        get => useSeparateAxes;
        set => Set(ref useSeparateAxes, value);
    }

    [Display(GroupName = nameof(Texts.Scale_tab), Name = nameof(Texts.Vertical), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", -100, 100)]
    public Animation Vertical { get; } = new(100, -ParameterLimit, ParameterLimit);

    [Display(GroupName = nameof(Texts.Scale_tab), Name = nameof(Texts.Horizontal), ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", -100, 100)]
    public Animation Horizontal { get; } = new(100, -ParameterLimit, ParameterLimit);

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        => new ProportionalScaleProcessor(devices, this);

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    protected override IEnumerable<IAnimatable> GetAnimatables() => [Overall, Vertical, Horizontal];
}
