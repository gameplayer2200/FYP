
#if UNITY_POST_PROCESSING_STACK_V2
using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

[Serializable]
[PostProcess( typeof( GBCameraPPSRenderer ), PostProcessEvent.AfterStack, "RogueNoodle/GBCamera", true )]
public sealed class GBCameraSettings : PostProcessEffectSettings
{
	[Tooltip( "Palette" )]
	public TextureParameter _Palette = new TextureParameter {  };
	[Tooltip( "Res X" )]
	public FloatParameter _ResX = new FloatParameter { value = 160f };
	[Tooltip( "Res Y" )]
	public FloatParameter _ResY = new FloatParameter { value = 144f };
	[Tooltip( "Fade" )]
	public FloatParameter _Fade = new FloatParameter { value = 1f };
}

public sealed class GBCameraPPSRenderer : PostProcessEffectRenderer<GBCameraSettings>
{
	public override void Render( PostProcessRenderContext context )
	{
		var sheet = context.propertySheets.Get( Shader.Find( "RogueNoodle/GBCamera_PPS" ) );
		if(settings._Palette.value != null) sheet.properties.SetTexture( "_Palette", settings._Palette );
		sheet.properties.SetFloat( "_ResX", settings._ResX );
		sheet.properties.SetFloat( "_ResY", settings._ResY );
		sheet.properties.SetFloat( "_Fade", settings._Fade );
		context.command.BlitFullscreenTriangle( context.source, context.destination, sheet, 0 );
	}
}
#endif
