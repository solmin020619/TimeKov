// Made with Amplify Shader Editor v1.9.9.5
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "VattalusAssets/Standard_Tinted_Transparent"
{
	Properties
	{
		_Cutoff( "Mask Clip Value", Float ) = 0.5
		_Albedo( "Albedo", 2D ) = "white" {}
		_MetalRoughAO( "MetalRoughAO", 2D ) = "white" {}
		_NormalMap( "NormalMap", 2D ) = "bump" {}
		_Emissive( "Emissive", 2D ) = "black" {}
		_EmissionColor( "EmissionColor", Color ) = ( 1, 1, 1, 0 )
		_TintMap( "TintMap", 2D ) = "black" {}
		_Tint_Color1( "Tint_Color1", Color ) = ( 1, 1, 1, 0 )
		_Tint_Color2( "Tint_Color2", Color ) = ( 1, 1, 1, 0 )
		_Tint_Color3( "Tint_Color3", Color ) = ( 1, 1, 1, 0 )
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "TransparentCutout"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Back
		ZTest LEqual
		CGPROGRAM
		#pragma target 3.0
		#define ASE_VERSION 19905
		#pragma exclude_renderers xboxseries playstation switch 
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float2 uv_texcoord;
		};

		uniform sampler2D _NormalMap;
		uniform float4 _NormalMap_ST;
		uniform sampler2D _Albedo;
		uniform float4 _Albedo_ST;
		uniform float4 _Tint_Color1;
		uniform sampler2D _TintMap;
		uniform float4 _TintMap_ST;
		uniform float4 _Tint_Color2;
		uniform float4 _Tint_Color3;
		uniform sampler2D _Emissive;
		uniform float4 _Emissive_ST;
		uniform float4 _EmissionColor;
		uniform sampler2D _MetalRoughAO;
		uniform float4 _MetalRoughAO_ST;
		uniform float _Cutoff = 0.5;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_NormalMap = i.uv_texcoord * _NormalMap_ST.xy + _NormalMap_ST.zw;
			o.Normal = UnpackNormal( tex2D( _NormalMap, uv_NormalMap ) );
			float2 uv_Albedo = i.uv_texcoord * _Albedo_ST.xy + _Albedo_ST.zw;
			float4 tex2DNode342 = tex2D( _Albedo, uv_Albedo );
			float2 uv_TintMap = i.uv_texcoord * _TintMap_ST.xy + _TintMap_ST.zw;
			float4 tex2DNode377 = tex2D( _TintMap, uv_TintMap );
			float4 lerpResult387 = lerp( tex2DNode342 , ( ( _Tint_Color1 * tex2DNode377.r ) + ( _Tint_Color2 * tex2DNode377.g ) + ( _Tint_Color3 * tex2DNode377.b ) ) , ( tex2DNode377.r + tex2DNode377.g + tex2DNode377.b ));
			o.Albedo = lerpResult387.rgb;
			float2 uv_Emissive = i.uv_texcoord * _Emissive_ST.xy + _Emissive_ST.zw;
			o.Emission = ( tex2D( _Emissive, uv_Emissive ) * _EmissionColor ).rgb;
			float2 uv_MetalRoughAO = i.uv_texcoord * _MetalRoughAO_ST.xy + _MetalRoughAO_ST.zw;
			float4 tex2DNode372 = tex2D( _MetalRoughAO, uv_MetalRoughAO );
			o.Metallic = tex2DNode372.r;
			o.Smoothness = ( 1.0 - tex2DNode372.g );
			o.Occlusion = tex2DNode372.b;
			o.Alpha = 1;
			clip( tex2DNode342.a - _Cutoff );
		}

		ENDCG
	}
	Fallback "Standard"
}
/*ASEBEGIN
Version=19905
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;377;971.3458,-136.1345;Inherit;True;Property;_TintMap;TintMap;6;0;Create;True;0;0;0;False;0;False;341;None;ead19c23fc8800d4c970f93d720544ce;True;0;False;black;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;380;1059.746,-309.0338;Inherit;False;Property;_Tint_Color3;Tint_Color3;9;0;Create;True;0;0;0;False;0;False;1,1,1,0;0.3333333,0.3333333,0.3333333,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;379;1061.046,-480.634;Inherit;False;Property;_Tint_Color2;Tint_Color2;8;0;Create;True;0;0;0;False;0;False;1,1,1,0;0.2470588,0.317647,0.4509804,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;378;1061.046,-649.6341;Inherit;False;Property;_Tint_Color1;Tint_Color1;7;0;Create;True;0;0;0;False;0;False;1,1,1,0;0.7215686,0.490196,0.2666667,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;383;1343.845,-364.5338;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;382;1345.245,-474.6335;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;381;1348.246,-570.434;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;386;1392.137,-106.8693;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;341;974.6098,469.1712;Inherit;True;Property;_Emissive;Emissive;4;0;Create;True;0;0;0;False;0;False;-1;None;645a2a13430b0c94f801e8be0619b1f7;True;0;False;black;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;384;1523.617,-499.3961;Inherit;False;3;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;342;1407.386,24.4118;Inherit;True;Property;_Albedo;Albedo;1;0;Create;True;0;0;0;False;0;False;342;None;28834b219d02c7940a68d0f81d0055c2;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;370;1058.872,660.4412;Inherit;False;Property;_EmissionColor;EmissionColor;5;0;Create;True;0;0;0;False;0;False;1,1,1,0;1,1,1,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;372;975.5171,853.3292;Inherit;True;Property;_MetalRoughAO;MetalRoughAO;2;0;Create;True;0;0;0;False;0;False;-1;None;2cd14f23856086846b65be8a4752a163;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;340;976.5139,272.1596;Inherit;True;Property;_NormalMap;NormalMap;3;0;Create;True;0;0;0;False;0;False;-1;None;bd53834390004bb4c812a29752b86758;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;1;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;371;1322.772,473.2411;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;387;1755.965,-394.2052;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;373;1365.517,906.6293;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;0;1956.572,178.2452;Float;False;True;-1;2;;0;0;Standard;VattalusAssets/Standard_Tinted_Transparent;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;;3;False;;False;0;False;;0;False;;False;0;Custom;0.5;True;True;0;True;TransparentCutout;;Geometry;All;9;d3d11;glcore;gles;gles3;metal;vulkan;xboxone;ps4;ps5;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;0;4;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;1;False;;1;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;Standard;0;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;383;0;380;0
WireConnection;383;1;377;3
WireConnection;382;0;379;0
WireConnection;382;1;377;2
WireConnection;381;0;378;0
WireConnection;381;1;377;1
WireConnection;386;0;377;1
WireConnection;386;1;377;2
WireConnection;386;2;377;3
WireConnection;384;0;381;0
WireConnection;384;1;382;0
WireConnection;384;2;383;0
WireConnection;371;0;341;0
WireConnection;371;1;370;0
WireConnection;387;0;342;0
WireConnection;387;1;384;0
WireConnection;387;2;386;0
WireConnection;373;0;372;2
WireConnection;0;0;387;0
WireConnection;0;1;340;0
WireConnection;0;2;371;0
WireConnection;0;3;372;1
WireConnection;0;4;373;0
WireConnection;0;5;372;3
WireConnection;0;10;342;4
ASEEND*/
//CHKSM=ADD12BC786CADE2A4E09F423E6EA9D51B2A09345