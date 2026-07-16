// Made with Amplify Shader Editor v1.9.9.5
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "VattalusAssets/HyperspaceShellShader"
{
	Properties
	{
		_GlobalOpacity( "GlobalOpacity", Range( 0, 1 ) ) = 0
		[Header(Transition Parameters)] _Transition( "Transition", Range( -1, 1 ) ) = 0
		[NoScaleOffset] _TransitionGradient( "TransitionGradient", 2D ) = "white" {}
		[Header(Shell Opacity Parameters)] _ShellOpacity_Value( "ShellOpacity_Value", Range( 0, 1 ) ) = 0
		[NoScaleOffset] _ShellOpacity_Texture( "ShellOpacity_Texture", 2D ) = "white" {}
		_ShellOpacity_SpeedTiling( "ShellOpacity_Speed&Tiling", Vector ) = ( 0, 0, 1, 1 )
		[Header(Color gradient applied to all layers (Multiply))] _ColorGradient_Texture( "ColorGradient_Texture", 2D ) = "white" {}
		[HDR][Header(Layer 1 Parameters)] _Layer1_Color( "Layer1_Color", Color ) = ( 0.254539, 0.6732539, 0.9811321, 1 )
		[NoScaleOffset] _Layer1_Texture( "Layer1_Texture", 2D ) = "white" {}
		_Layer1_SpeedTiling( "Layer1_Speed&Tiling", Vector ) = ( 5, 1, 1, 1 )
		[HDR][Header(Layer 2 Parameters)] _Layer2_Color( "Layer2_Color", Color ) = ( 0.254539, 0.6732539, 0.9811321, 1 )
		[NoScaleOffset] _Layer2_Texture( "Layer2_Texture", 2D ) = "white" {}
		_Layer2_SpeedTiling( "Layer2_Speed&Tiling", Vector ) = ( 5, 1, 1, 1 )
		[HDR][Header(Light1 Parameters)] _Light1_Color( "Light1_Color", Color ) = ( 0.254539, 0.6732539, 0.9811321, 1 )
		[NoScaleOffset] _Glow1_Texture( "Glow1_Texture", 2D ) = "white" {}
		_Light1_SpeedTiling( "Light1_Speed&Tiling", Vector ) = ( 5, 1, 1, 1 )
		[HDR][Header(Light2 Parameters)] _Light2_Color( "Light2_Color", Color ) = ( 0.254539, 0.6732539, 0.9811321, 1 )
		[NoScaleOffset] _Glow2_Texture( "Glow2_Texture", 2D ) = "white" {}
		_Light2_SpeedTiling( "Light2_Speed&Tiling", Vector ) = ( 0, 0, 1, 1 )
		[Header(Flashing Effect (driven by script))] _FlashingTexture( "FlashingTexture", 2D ) = "white" {}
		[HDR] _FlashingColor( "FlashingColor", Color ) = ( 0.254539, 0.6732539, 0.9811321, 1 )
		[Header(Displacement Parameters (Applied to shell mesh))][NoScaleOffset] _Displacement_Texture( "Displacement_Texture", 2D ) = "white" {}
		_Displacement_Intensity( "Displacement_Intensity", Range( -1, 1 ) ) = 0
		_Displacement_Scale( "Displacement_Scale", Vector ) = ( 40, 40, 0, 0 )
		_Displacement_SpeedTiling( "Displacement_Speed&Tiling", Vector ) = ( 0, 0, 1, 1 )
		[Header(Distortion Parameters (Applied to background))][NoScaleOffset][Normal] _Distortion_Texture( "Distortion_Texture", 2D ) = "bump" {}
		_Distortion_Intensity( "Distortion_Intensity", Range( 0, 1 ) ) = 0
		_Distortion_SpeedTiling( "Distortion_Speed&Tiling", Vector ) = ( 5, 1, 1, 1 )
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True" "IsEmissive" = "true"  }
		Cull Back
		GrabPass{ }
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#include "UnityStandardUtils.cginc"
		#include "UnityStandardBRDF.cginc"
		#pragma target 3.0
		#define ASE_VERSION 19905
		#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex);
		#else
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex)
		#endif
		#pragma surface surf Unlit alpha:fade keepalpha noshadow noambient novertexlights nolightmap  nodirlightmap vertex:vertexDataFunc 
		struct Input
		{
			float3 worldNormal;
			INTERNAL_DATA
			float2 uv_texcoord;
			float3 worldPos;
			float4 screenPos;
		};

		uniform float _Displacement_Intensity;
		uniform sampler2D _Displacement_Texture;
		uniform float4 _Displacement_SpeedTiling;
		uniform float3 _Displacement_Scale;
		uniform sampler2D _Distortion_Texture;
		uniform float4 _Distortion_SpeedTiling;
		uniform float _Distortion_Intensity;
		ASE_DECLARE_SCREENSPACE_TEXTURE( _GrabTexture )
		uniform sampler2D _FlashingTexture;
		uniform float4 _FlashingTexture_ST;
		uniform float4 _FlashingColor;
		uniform sampler2D _ColorGradient_Texture;
		uniform float4 _ColorGradient_Texture_ST;
		uniform float4 _Layer1_Color;
		uniform sampler2D _Layer1_Texture;
		uniform float4 _Layer1_SpeedTiling;
		uniform float4 _Layer2_Color;
		uniform sampler2D _Layer2_Texture;
		uniform float4 _Layer2_SpeedTiling;
		uniform sampler2D _Glow1_Texture;
		uniform float4 _Light1_SpeedTiling;
		uniform float4 _Light1_Color;
		uniform sampler2D _Glow2_Texture;
		uniform float4 _Light2_SpeedTiling;
		uniform float4 _Light2_Color;
		uniform sampler2D _ShellOpacity_Texture;
		uniform float4 _ShellOpacity_SpeedTiling;
		uniform float _ShellOpacity_Value;
		uniform float _GlobalOpacity;
		uniform sampler2D _TransitionGradient;
		uniform float _Transition;


		inline float4 ASE_ComputeGrabScreenPos( float4 pos )
		{
			#if UNITY_UV_STARTS_AT_TOP
			float scale = -1.0;
			#else
			float scale = 1.0;
			#endif
			float4 o = pos;
			o.y = pos.w * 0.5f;
			o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
			return o;
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float2 appendResult292 = (float2(_Displacement_SpeedTiling.z , _Displacement_SpeedTiling.w));
			float4 Time64 = _Time;
			float2 appendResult293 = (float2(( _Displacement_SpeedTiling.x * Time64 ).x , ( _Displacement_SpeedTiling.y * Time64 ).x));
			float2 uv_TexCoord294 = v.texcoord.xy * appendResult292 + appendResult293;
			float4 tex2DNode97 = tex2Dlod( _Displacement_Texture, float4( uv_TexCoord294, 0, 1.0) );
			float4 appendResult207 = (float4(_Displacement_Scale.x , _Displacement_Scale.z , _Displacement_Scale.y , 0.0));
			float3 ase_normalOS = v.normal.xyz;
			v.vertex.xyz += ( _Displacement_Intensity * float4( tex2DNode97.rgb , 0.0 ) * appendResult207 * float4( ase_normalOS , 0.0 ) ).xyz;
			v.vertex.w = 1;
		}

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			o.Normal = float3(0,0,1);
			float2 appendResult300 = (float2(_Distortion_SpeedTiling.z , _Distortion_SpeedTiling.w));
			float4 Time64 = _Time;
			float2 appendResult301 = (float2(( _Distortion_SpeedTiling.x * Time64 ).x , ( _Distortion_SpeedTiling.y * Time64 ).x));
			float2 uv_TexCoord302 = i.uv_texcoord * appendResult300 + appendResult301;
			float3 tex2DNode10 = UnpackScaleNormal( tex2D( _Distortion_Texture, uv_TexCoord302 ), _Distortion_Intensity );
			float3 ase_positionWS = i.worldPos;
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_positionWS );
			float3 ase_viewDirSafeWS = Unity_SafeNormalize( ase_viewVectorWS );
			float dotResult25 = dot( normalize( (WorldNormalVector( i , tex2DNode10 )) ) , ase_viewDirSafeWS );
			float4 ase_positionSS = float4( i.screenPos.xyz , i.screenPos.w + 1e-7 );
			float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ase_positionSS );
			float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
			float4 screenColor8 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_GrabTexture,( ase_grabScreenPosNorm + float4( tex2DNode10 , 0.0 ) ).xy/( ase_grabScreenPosNorm + float4( tex2DNode10 , 0.0 ) ).w);
			float2 uv_FlashingTexture = i.uv_texcoord * _FlashingTexture_ST.xy + _FlashingTexture_ST.zw;
			float2 uv_ColorGradient_Texture = i.uv_texcoord * _ColorGradient_Texture_ST.xy + _ColorGradient_Texture_ST.zw;
			float4 tex2DNode161 = tex2D( _ColorGradient_Texture, uv_ColorGradient_Texture );
			float2 appendResult263 = (float2(_Layer1_SpeedTiling.z , _Layer1_SpeedTiling.w));
			float2 appendResult266 = (float2(( _Layer1_SpeedTiling.x * Time64 ).x , ( _Layer1_SpeedTiling.y * Time64 ).x));
			float2 uv_TexCoord262 = i.uv_texcoord * appendResult263 + appendResult266;
			float2 appendResult260 = (float2(_Layer2_SpeedTiling.z , _Layer2_SpeedTiling.w));
			float2 appendResult259 = (float2(( _Layer2_SpeedTiling.x * Time64 ).x , ( _Layer2_SpeedTiling.y * Time64 ).x));
			float2 uv_TexCoord254 = i.uv_texcoord * appendResult260 + appendResult259;
			float2 appendResult292 = (float2(_Displacement_SpeedTiling.z , _Displacement_SpeedTiling.w));
			float2 appendResult293 = (float2(( _Displacement_SpeedTiling.x * Time64 ).x , ( _Displacement_SpeedTiling.y * Time64 ).x));
			float2 uv_TexCoord294 = i.uv_texcoord * appendResult292 + appendResult293;
			float4 tex2DNode97 = tex2D( _Displacement_Texture, uv_TexCoord294 );
			float3 ifLocalVar218 = 0;
			if( _Displacement_Intensity >= 0.0 )
				ifLocalVar218 = tex2DNode97.rgb;
			else
				ifLocalVar218 = ( 1.0 - tex2DNode97.rgb );
			float3 lerpResult210 = lerp( float3( 0.9,0.9,0.9 ) , ifLocalVar218 , abs( _Displacement_Intensity ));
			float2 appendResult272 = (float2(_Light1_SpeedTiling.z , _Light1_SpeedTiling.w));
			float2 appendResult273 = (float2(( _Light1_SpeedTiling.x * Time64 ).x , ( _Light1_SpeedTiling.y * Time64 ).x));
			float2 uv_TexCoord274 = i.uv_texcoord * appendResult272 + appendResult273;
			float2 appendResult278 = (float2(_Light2_SpeedTiling.z , _Light2_SpeedTiling.w));
			float2 appendResult279 = (float2(( _Light2_SpeedTiling.x * Time64 ).x , ( _Light2_SpeedTiling.y * Time64 ).x));
			float2 uv_TexCoord280 = i.uv_texcoord * appendResult278 + appendResult279;
			float2 appendResult286 = (float2(_ShellOpacity_SpeedTiling.z , _ShellOpacity_SpeedTiling.w));
			float2 appendResult287 = (float2(( _ShellOpacity_SpeedTiling.x * Time64 ).x , ( _ShellOpacity_SpeedTiling.y * Time64 ).x));
			float2 uv_TexCoord288 = i.uv_texcoord * appendResult286 + appendResult287;
			float4 tex2DNode129 = tex2D( _ShellOpacity_Texture, uv_TexCoord288 );
			float3 lerpResult197 = lerp( ( tex2DNode129.rgb * tex2DNode129.rgb * _ShellOpacity_Value ) , float3( 1,1,1 ) , _ShellOpacity_Value);
			float4 lerpResult138 = lerp( ( ( 1.0 - ( dotResult25 * 0.8 ) ) * screenColor8 ) , float4( ( ( tex2D( _FlashingTexture, uv_FlashingTexture ).rgb * _FlashingColor.rgb * tex2DNode161.rgb ) + ( ( _Layer1_Color.rgb * tex2D( _Layer1_Texture, uv_TexCoord262 ).rgb ) * ( _Layer2_Color.rgb * tex2D( _Layer2_Texture, uv_TexCoord254 ).rgb ) * tex2DNode161.rgb * lerpResult210 ) + ( tex2DNode161.rgb * ( tex2D( _Glow1_Texture, uv_TexCoord274 ).rgb * _Light1_Color.rgb ) ) + ( tex2DNode161.rgb * ( tex2D( _Glow2_Texture, uv_TexCoord280 ).rgb * _Light2_Color.rgb ) ) ) , 0.0 ) , float4( lerpResult197 , 0.0 ));
			o.Emission = lerpResult138.rgb;
			float ifLocalVar172 = 0;
			if( _Transition <= 0.0 )
				ifLocalVar172 = 1.0;
			else
				ifLocalVar172 = -1.0;
			float2 appendResult174 = (float2(ifLocalVar172 , 1.0));
			float temp_output_225_0 = abs( _Transition );
			float lerpResult227 = lerp( 2.0 , 0.0 , temp_output_225_0);
			float lerpResult226 = lerp( 1.0 , -1.0 , temp_output_225_0);
			float ifLocalVar222 = 0;
			if( _Transition <= 0.0 )
				ifLocalVar222 = lerpResult226;
			else
				ifLocalVar222 = lerpResult227;
			float2 appendResult224 = (float2(ifLocalVar222 , 1.0));
			float2 uv_TexCoord166 = i.uv_texcoord * appendResult174 + appendResult224;
			o.Alpha = ( _GlobalOpacity * tex2D( _TransitionGradient, uv_TexCoord166 ).rgb ).x;
		}

		ENDCG
	}
	Fallback Off
	CustomEditor "AmplifyShaderEditor.MaterialInspector"
}
/*ASEBEGIN
Version=19905
Node;AmplifyShaderEditor.TimeNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;42;-1968,-1120;Inherit;False;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;303;-1968,-848;Inherit;False;2129.721;696.8318;Scrolling texture multiplied with layer 2;15;27;29;30;34;32;10;31;13;302;301;300;297;299;298;296;Background Distortion;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;64;-1728,-1120;Inherit;False;Time;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;159;-2384,3088;Inherit;False;1936.774;568.3193;Scrolling displacement effect;17;202;96;207;236;210;218;216;97;220;99;294;293;292;291;290;295;289;Displacement;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;154;-2064,1952;Inherit;False;1525.661;388.0885;Optionally Scrolling Opacity Map;11;139;129;197;200;282;283;284;285;286;287;288;Shell Opacity;1,1,1,1;0;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;296;-1888,-480;Inherit;False;64;Time;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;289;-2352,3360;Inherit;False;64;Time;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;157;-1968,528;Inherit;False;1427.188;441.1239;Scrolling texture multiplied with layer 1;10;72;255;259;257;256;260;254;258;74;73;Layer 2;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;158;-1968,-16;Inherit;False;1427.188;435.1239;Scrolling texture multiplied with layer 2;10;45;47;46;261;262;263;264;265;266;267;Layer 1;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;155;-2032,1040;Inherit;False;1570.605;371.5564;Scrolling Glow Effect that is applied as an additive layer on the shell;10;269;274;273;272;271;270;268;88;248;86;Light Layer 1;1,1,1,1;0;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;282;-2032,2224;Inherit;False;64;Time;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;298;-1616,-496;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;299;-1616,-592;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.Vector4Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;297;-1952,-688;Inherit;False;Property;_Distortion_SpeedTiling;Distortion_Speed&Tiling;27;0;Create;True;0;0;0;False;0;False;5,1,1,1;40,5,2,4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;237;-2112,1488;Inherit;False;1569.588;386.3749;Scrolling Glow Effect that is applied as an additive layer on the shell;10;247;249;245;275;276;277;278;279;280;281;Light Layer 2;1,1,1,1;0;0
Node;AmplifyShaderEditor.Vector4Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;295;-2368,3152;Inherit;False;Property;_Displacement_SpeedTiling;Displacement_Speed&Tiling;24;0;Create;True;0;0;0;False;0;False;0,0,1,1;6,1,1,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;290;-2032,3344;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;291;-2032,3248;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;255;-1936,816;Inherit;False;64;Time;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;268;-1984,1296;Inherit;False;64;Time;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;267;-1936,256;Inherit;False;64;Time;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;284;-1728,2208;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;285;-1728,2112;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.Vector4Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;283;-2048,2016;Inherit;False;Property;_ShellOpacity_SpeedTiling;ShellOpacity_Speed&Tiling;5;0;Create;True;0;0;0;False;0;False;0,0,1,1;15,1,3,2;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;300;-1616,-688;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;301;-1456,-496;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;275;-2064,1744;Inherit;False;64;Time;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;292;-2032,3152;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;293;-1872,3344;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;229;-2192,2416;Inherit;False;1656.394;580.9997;Applies an opacity gradient that is offset to achieve smooth in/out transition animations;12;165;166;224;174;222;172;176;175;164;227;226;225;Transition;1,1,1,1;0;0
Node;AmplifyShaderEditor.Vector4Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;258;-1952,608;Inherit;False;Property;_Layer2_SpeedTiling;Layer2_Speed&Tiling;12;0;Create;True;0;0;0;False;0;False;5,1,1,1;5,1,1,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;270;-1712,1280;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;271;-1712,1184;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;256;-1664,800;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;257;-1664,704;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.Vector4Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;261;-1952,48;Inherit;False;Property;_Layer1_SpeedTiling;Layer1_Speed&Tiling;9;0;Create;True;0;0;0;False;0;False;5,1,1,1;10,1,1,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;264;-1664,240;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;265;-1664,144;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;286;-1728,2016;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;287;-1568,2208;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;302;-1264,-544;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;13;-1344,-352;Float;False;Property;_Distortion_Intensity;Distortion_Intensity;26;0;Create;True;0;0;0;False;0;False;0;0.1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;269;-2000,1088;Inherit;False;Property;_Light1_SpeedTiling;Light1_Speed&Tiling;15;0;Create;True;0;0;0;False;0;False;5,1,1,1;15,1,1,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;276;-1792,1728;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;277;-1792,1632;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.Vector4Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;281;-2080,1536;Inherit;False;Property;_Light2_SpeedTiling;Light2_Speed&Tiling;18;0;Create;True;0;0;0;False;0;False;0,0,1,1;0,0,1,1;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;294;-1712,3232;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;31;-624,-800;Inherit;False;348.399;353.3;Traditional nDotV to fake thickness;3;25;28;26;;1,1,1,1;0;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;272;-1712,1088;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;273;-1552,1280;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;260;-1664,608;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;259;-1504,800;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;263;-1664,48;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;266;-1504,240;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;288;-1376,2160;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.AbsOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;225;-1856,2752;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;10;-960,-560;Inherit;True;Property;_Distortion_Texture;Distortion_Texture;25;3;[Header];[NoScaleOffset];[Normal];Create;True;1;Distortion Parameters (Applied to background);0;0;False;0;False;-1;None;2d3780f2fff4ed54c84980d0e7dc23c5;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;278;-1792,1536;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;279;-1632,1728;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;220;-992,3264;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;97;-1488,3152;Inherit;True;Property;_Displacement_Texture;Displacement_Texture;21;2;[Header];[NoScaleOffset];Create;True;1;Displacement Parameters (Applied to shell mesh);0;0;False;0;False;-1;9789d23040cb1fb45ad60392430c3c15;42bfc58efd5837245bf4fb6f2f84b951;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;32;-624,-432;Inherit;False;583.7003;262.8998;Simple Refraction with normal perturbance;4;12;8;35;9;;1,1,1,1;0;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;254;-1312,752;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;274;-1360,1232;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;262;-1312,192;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;129;-1152,2016;Inherit;True;Property;_ShellOpacity_Texture;ShellOpacity_Texture;4;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;9789d23040cb1fb45ad60392430c3c15;d26f7e2abc733454c9f3fed510645e5c;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;139;-1152,2224;Inherit;False;Property;_ShellOpacity_Value;ShellOpacity_Value;3;1;[Header];Create;True;1;Shell Opacity Parameters;0;0;False;0;False;0;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;226;-1696,2864;Inherit;False;3;0;FLOAT;1;False;1;FLOAT;-1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;227;-1696,2704;Inherit;False;3;0;FLOAT;2;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;164;-2176,2464;Float;False;Property;_Transition;Transition;1;1;[Header];Create;True;1;Transition Parameters;0;0;False;0;False;0;0;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;175;-2048,2544;Inherit;False;Constant;_Float3;Float 1;29;0;Create;True;0;0;0;False;0;False;-1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;176;-2048,2624;Inherit;False;Constant;_Float4;Float 1;29;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;34;-656,-240;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WorldNormalVector, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;26;-576,-752;Inherit;False;True;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ViewDirInputsCoordNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;28;-576,-608;Float;False;World;True;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;99;-1488,3360;Float;False;Property;_Displacement_Intensity;Displacement_Intensity;22;0;Create;True;0;0;0;False;0;False;0;1;-1;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;280;-1440,1680;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.AbsOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;216;-816,3312;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ConditionalIfNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;218;-816,3136;Inherit;False;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;73;-1008,576;Inherit;False;Property;_Layer2_Color;Layer2_Color;10;2;[HDR];[Header];Create;True;1;Layer 2 Parameters;0;0;False;0;False;0.254539,0.6732539,0.9811321,1;0.5405639,0.8829211,1.130679,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;46;-1008,32;Inherit;False;Property;_Layer1_Color;Layer1_Color;7;2;[HDR];[Header];Create;True;1;Layer 1 Parameters;0;0;False;0;False;0.254539,0.6732539,0.9811321,1;1.976471,2.572549,2.996078,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;72;-1072,768;Inherit;True;Property;_Layer2_Texture;Layer2_Texture;11;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;None;0b7fb35b78b4b68429e7f418eb489347;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;45;-1072,224;Inherit;True;Property;_Layer1_Texture;Layer1_Texture;8;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;None;3f77e33f3fa2f6942920ac3e0bcbef6a;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;200;-848,2032;Inherit;False;3;3;0;FLOAT3;0,0,0;False;1;FLOAT3;1,1,1;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ConditionalIfNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;172;-1488,2464;Inherit;False;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ConditionalIfNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;222;-1488,2656;Inherit;False;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;35;-624,-208;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DotProductOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;25;-384,-672;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GrabScreenPosition, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;9;-576,-384;Inherit;False;0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;161;-352,944;Inherit;True;Property;_ColorGradient_Texture;ColorGradient_Texture;6;1;[Header];Create;True;1;Color gradient applied to all layers (Multiply);0;0;False;0;False;-1;None;457c019dcc2c63245bddcdd3e1752448;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;86;-1104,1088;Inherit;True;Property;_Glow1_Texture;Glow1_Texture;14;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;None;7e148ebed6bf2c946a3e0e70e53f1ea4;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;248;-832,1184;Inherit;False;Property;_Light1_Color;Light1_Color;13;2;[HDR];[Header];Create;True;1;Light1 Parameters;0;0;False;0;False;0.254539,0.6732539,0.9811321,1;1.756863,2.807843,4,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;245;-1184,1536;Inherit;True;Property;_Glow2_Texture;Glow2_Texture;17;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;None;641468e24de344a4e86e64129ad5b8da;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;249;-912,1632;Inherit;False;Property;_Light2_Color;Light2_Color;16;2;[HDR];[Header];Create;True;1;Light2 Parameters;0;0;False;0;False;0.254539,0.6732539,0.9811321,1;1.474815,2.635549,3.482202,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;210;-592,3184;Inherit;False;3;0;FLOAT3;0.9,0.9,0.9;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;74;-688,688;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;47;-688,96;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;197;-672,2032;Inherit;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;1,1,1;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;174;-1248,2464;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;224;-1248,2656;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector3Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;236;-1488,3440;Inherit;False;Property;_Displacement_Scale;Displacement_Scale;23;0;Create;True;0;0;0;False;0;False;40,40,0;40,40,0;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;88;-608,1104;Inherit;False;2;2;0;FLOAT3;1,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ScaleNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;30;-256,-672;Inherit;False;0.8;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;12;-336,-272;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;304;208,720;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;235;0,400;Inherit;False;Property;_FlashingColor;FlashingColor;20;1;[HDR];Create;True;0;0;0;False;0;False;0.254539,0.6732539,0.9811321,1;0.6496802,1.07699,1.480991,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;233;-96,192;Inherit;True;Property;_FlashingTexture;FlashingTexture;19;1;[Header];Create;True;1;Flashing Effect (driven by script);0;0;False;0;False;-1;None;32b9138f60e8f024aa4478ec708c4590;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;247;-672,1552;Inherit;False;2;2;0;FLOAT3;1,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;166;-1056,2528;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.DynamicAppendNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;207;-1264,3440;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.NormalVertexDataNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;96;-1040,3504;Inherit;False;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;231;880,1536;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;75;224,912;Inherit;False;4;4;0;FLOAT3;0,0,0;False;1;FLOAT3;1,1,1;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;29;-112,-672;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScreenColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;8;-208,-352;Float;False;Global;_GrabScreen0;Grab Screen 0;1;0;Create;True;0;0;0;False;0;False;Object;-1;False;True;False;False;2;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;234;272,400;Inherit;False;3;3;0;FLOAT3;0,0,0;False;1;FLOAT3;2,2,2;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;250;-48,1072;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;305;-48,1520;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;202;-816,3392;Inherit;False;4;4;0;FLOAT;1;False;1;FLOAT3;1,0,0;False;2;FLOAT4;0,0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;165;-816,2496;Inherit;True;Property;_TransitionGradient;TransitionGradient;2;1;[NoScaleOffset];Create;True;0;0;0;False;0;False;-1;None;ee3adfd3ae6f50348a1d4c308e99f177;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;79;928,1584;Inherit;False;Property;_GlobalOpacity;GlobalOpacity;0;0;Create;True;0;0;0;False;0;False;0;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;230;944,1520;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;90;560,1472;Inherit;False;4;4;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;27;16,-480;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;204;-336,3360;Inherit;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;138;1216,1440;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;171;1216,1584;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;0;1392,1392;Float;False;True;-1;2;AmplifyShaderEditor.MaterialInspector;0;0;Unlit;VattalusAssets/HyperspaceShellShader;False;False;False;False;True;True;True;False;True;False;False;False;False;False;True;True;False;False;False;False;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;Transparent;0.5;True;False;0;False;Transparent;;Transparent;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;False;2;5;False;;10;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;64;0;42;0
WireConnection;298;0;297;2
WireConnection;298;1;296;0
WireConnection;299;0;297;1
WireConnection;299;1;296;0
WireConnection;290;0;295;2
WireConnection;290;1;289;0
WireConnection;291;0;295;1
WireConnection;291;1;289;0
WireConnection;284;0;283;2
WireConnection;284;1;282;0
WireConnection;285;0;283;1
WireConnection;285;1;282;0
WireConnection;300;0;297;3
WireConnection;300;1;297;4
WireConnection;301;0;299;0
WireConnection;301;1;298;0
WireConnection;292;0;295;3
WireConnection;292;1;295;4
WireConnection;293;0;291;0
WireConnection;293;1;290;0
WireConnection;270;0;269;2
WireConnection;270;1;268;0
WireConnection;271;0;269;1
WireConnection;271;1;268;0
WireConnection;256;0;258;2
WireConnection;256;1;255;0
WireConnection;257;0;258;1
WireConnection;257;1;255;0
WireConnection;264;0;261;2
WireConnection;264;1;267;0
WireConnection;265;0;261;1
WireConnection;265;1;267;0
WireConnection;286;0;283;3
WireConnection;286;1;283;4
WireConnection;287;0;285;0
WireConnection;287;1;284;0
WireConnection;302;0;300;0
WireConnection;302;1;301;0
WireConnection;276;0;281;2
WireConnection;276;1;275;0
WireConnection;277;0;281;1
WireConnection;277;1;275;0
WireConnection;294;0;292;0
WireConnection;294;1;293;0
WireConnection;272;0;269;3
WireConnection;272;1;269;4
WireConnection;273;0;271;0
WireConnection;273;1;270;0
WireConnection;260;0;258;3
WireConnection;260;1;258;4
WireConnection;259;0;257;0
WireConnection;259;1;256;0
WireConnection;263;0;261;3
WireConnection;263;1;261;4
WireConnection;266;0;265;0
WireConnection;266;1;264;0
WireConnection;288;0;286;0
WireConnection;288;1;287;0
WireConnection;225;0;164;0
WireConnection;10;1;302;0
WireConnection;10;5;13;0
WireConnection;278;0;281;3
WireConnection;278;1;281;4
WireConnection;279;0;277;0
WireConnection;279;1;276;0
WireConnection;220;0;97;5
WireConnection;97;1;294;0
WireConnection;254;0;260;0
WireConnection;254;1;259;0
WireConnection;274;0;272;0
WireConnection;274;1;273;0
WireConnection;262;0;263;0
WireConnection;262;1;266;0
WireConnection;129;1;288;0
WireConnection;226;2;225;0
WireConnection;227;2;225;0
WireConnection;34;0;10;0
WireConnection;26;0;10;0
WireConnection;280;0;278;0
WireConnection;280;1;279;0
WireConnection;216;0;99;0
WireConnection;218;0;99;0
WireConnection;218;2;97;5
WireConnection;218;3;97;5
WireConnection;218;4;220;0
WireConnection;72;1;254;0
WireConnection;45;1;262;0
WireConnection;200;0;129;5
WireConnection;200;1;129;5
WireConnection;200;2;139;0
WireConnection;172;0;164;0
WireConnection;172;2;175;0
WireConnection;172;3;176;0
WireConnection;172;4;176;0
WireConnection;222;0;164;0
WireConnection;222;2;227;0
WireConnection;222;3;226;0
WireConnection;222;4;226;0
WireConnection;35;0;34;0
WireConnection;25;0;26;0
WireConnection;25;1;28;0
WireConnection;86;1;274;0
WireConnection;245;1;280;0
WireConnection;210;1;218;0
WireConnection;210;2;216;0
WireConnection;74;0;73;5
WireConnection;74;1;72;5
WireConnection;47;0;46;5
WireConnection;47;1;45;5
WireConnection;197;0;200;0
WireConnection;197;2;139;0
WireConnection;174;0;172;0
WireConnection;224;0;222;0
WireConnection;88;0;86;5
WireConnection;88;1;248;5
WireConnection;30;0;25;0
WireConnection;12;0;9;0
WireConnection;12;1;35;0
WireConnection;304;0;161;5
WireConnection;247;0;245;5
WireConnection;247;1;249;5
WireConnection;166;0;174;0
WireConnection;166;1;224;0
WireConnection;207;0;236;1
WireConnection;207;1;236;3
WireConnection;207;2;236;2
WireConnection;231;0;197;0
WireConnection;75;0;47;0
WireConnection;75;1;74;0
WireConnection;75;2;161;5
WireConnection;75;3;210;0
WireConnection;29;0;30;0
WireConnection;8;0;12;0
WireConnection;234;0;233;5
WireConnection;234;1;235;5
WireConnection;234;2;304;0
WireConnection;250;0;161;5
WireConnection;250;1;88;0
WireConnection;305;0;161;5
WireConnection;305;1;247;0
WireConnection;202;0;99;0
WireConnection;202;1;97;5
WireConnection;202;2;207;0
WireConnection;202;3;96;0
WireConnection;165;1;166;0
WireConnection;230;0;231;0
WireConnection;90;0;234;0
WireConnection;90;1;75;0
WireConnection;90;2;250;0
WireConnection;90;3;305;0
WireConnection;27;0;29;0
WireConnection;27;1;8;0
WireConnection;204;0;202;0
WireConnection;138;0;27;0
WireConnection;138;1;90;0
WireConnection;138;2;230;0
WireConnection;171;0;79;0
WireConnection;171;1;165;5
WireConnection;0;2;138;0
WireConnection;0;9;171;0
WireConnection;0;11;204;0
ASEEND*/
//CHKSM=93D8E05977D889759ABC862592B9DDF72193A3B8