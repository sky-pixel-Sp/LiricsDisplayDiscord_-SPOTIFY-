using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace SkyLyrics.Desktop.Effects;

public sealed class GrainShaderEffect : ShaderEffect
{
    private static readonly PixelShader GrainPixelShader = new()
    {
        UriSource = new Uri("/SkyLyrics.Desktop;component/Assets/Effects/Grain.ps", UriKind.Relative)
    };

    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty(nameof(Input), typeof(GrainShaderEffect), 0);

    public static readonly DependencyProperty IntensityProperty =
        DependencyProperty.Register(
            nameof(Intensity),
            typeof(double),
            typeof(GrainShaderEffect),
            new UIPropertyMetadata(0.018d, PixelShaderConstantCallback(0)));

    public static readonly DependencyProperty PhaseProperty =
        DependencyProperty.Register(
            nameof(Phase),
            typeof(double),
            typeof(GrainShaderEffect),
            new UIPropertyMetadata(0d, PixelShaderConstantCallback(1)));

    public GrainShaderEffect()
    {
        PixelShader = GrainPixelShader;
        UpdateShaderValue(InputProperty);
        UpdateShaderValue(IntensityProperty);
        UpdateShaderValue(PhaseProperty);
    }

    public System.Windows.Media.Brush? Input
    {
        get => (System.Windows.Media.Brush?)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    public double Intensity
    {
        get => (double)GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    public double Phase
    {
        get => (double)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }
}

public sealed class ChromaticAberrationShaderEffect : ShaderEffect
{
    private static readonly PixelShader ChromaticPixelShader = new()
    {
        UriSource = new Uri("/SkyLyrics.Desktop;component/Assets/Effects/ChromaticAberration.ps", UriKind.Relative)
    };

    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty(nameof(Input), typeof(ChromaticAberrationShaderEffect), 0);

    public static readonly DependencyProperty AmountProperty =
        DependencyProperty.Register(
            nameof(Amount),
            typeof(double),
            typeof(ChromaticAberrationShaderEffect),
            new UIPropertyMetadata(0.18d, PixelShaderConstantCallback(0)));

    public ChromaticAberrationShaderEffect()
    {
        PixelShader = ChromaticPixelShader;
        UpdateShaderValue(InputProperty);
        UpdateShaderValue(AmountProperty);
    }

    public System.Windows.Media.Brush? Input
    {
        get => (System.Windows.Media.Brush?)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    public double Amount
    {
        get => (double)GetValue(AmountProperty);
        set => SetValue(AmountProperty, value);
    }
}
