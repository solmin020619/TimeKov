// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "VattalusAssets/GlassExterior"
{
	Properties
	{
		_Color("Color", Color) = (0.6,0.6,0.6,0.2980392)
		_Specular("Specular", Range( 0 , 1)) = 0
		_RimStrength("RimStrength", Range( 0 , 5)) = 2
		_RimContrast("RimContrast", Range( 0 , 1)) = 0.5
		_Smoothness("Smoothness", Range( 0 , 1)) = 0.8
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+2" "IgnoreProjector" = "True" }
		Cull Back
		CGPROGRAM
		#pragma target 3.0
		#pragma surface surf StandardSpecular alpha:fade keepalpha exclude_path:deferred 
		struct Input
		{
			float3 worldNormal;
			float3 viewDir;
		};

		uniform float4 _Color;
		uniform float _Specular;
		uniform float _Smoothness;
		uniform float _RimStrength;
		uniform float _RimContrast;

		void surf( Input i , inout SurfaceOutputStandardSpecular o )
		{
			o.Albedo = _Color.rgb;
			float3 temp_cast_1 = (_Specular).xxx;
			o.Specular = temp_cast_1;
			o.Smoothness = _Smoothness;
			float3 ase_worldNormal = i.worldNormal;
			float dotResult3 = dot( ase_worldNormal , i.viewDir );
			float lerpResult18 = lerp( 0.25 , -0.15 , _RimContrast);
			o.Alpha = ( _Color.a * ( 1.0 - saturate( ( pow( dotResult3 , _RimStrength ) - lerpResult18 ) ) ) );
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=18935
-1080;-155;1080;1859;1709.367;546.1779;1;True;False
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;1;-1302.362,42.32227;Float;False;World;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldNormalVector;2;-1350.362,-101.6778;Inherit;False;False;1;0;FLOAT3;0,0,0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;16;-1316.923,204.1232;Float;False;Property;_RimStrength;RimStrength;2;0;Create;True;0;0;0;False;0;False;2;0.5;0;5;0;1;FLOAT;0
Node;AmplifyShaderEditor.DotProductOpNode;3;-1142.362,-37.67774;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;17;-1320.063,284.2063;Float;False;Property;_RimContrast;RimContrast;3;0;Create;True;0;0;0;False;0;False;0.5;0.15;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;4;-1014.362,-37.67774;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;18;-968.325,135.032;Inherit;False;3;0;FLOAT;0.25;False;1;FLOAT;-0.15;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;5;-854.3616,-37.67774;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0.1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;6;-710.361,-37.67774;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;15;-396.6414,-460.5572;Inherit;False;Property;_Color;Color;0;0;Create;True;0;0;0;False;0;False;0.6,0.6,0.6,0.2980392;0,0,0,1;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.OneMinusNode;8;-566.361,-37.67774;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;19;-234.7728,-59.72489;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;7;-627.6218,66.8451;Float;False;Property;_Specular;Specular;1;0;Create;True;0;0;0;False;0;False;0;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;12;-627.2739,143.5542;Float;False;Property;_Smoothness;Smoothness;4;0;Create;True;0;0;0;False;0;False;0.8;0.93;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;0,0;Float;False;True;-1;2;ASEMaterialInspector;0;0;StandardSpecular;VattalusAssets/GlassExterior;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Transparent;0.5;True;False;2;False;Transparent;;Transparent;ForwardOnly;18;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;2;5;False;-1;10;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;3;0;2;0
WireConnection;3;1;1;0
WireConnection;4;0;3;0
WireConnection;4;1;16;0
WireConnection;18;2;17;0
WireConnection;5;0;4;0
WireConnection;5;1;18;0
WireConnection;6;0;5;0
WireConnection;8;0;6;0
WireConnection;19;0;15;4
WireConnection;19;1;8;0
WireConnection;0;0;15;0
WireConnection;0;3;7;0
WireConnection;0;4;12;0
WireConnection;0;9;19;0
ASEEND*/
//CHKSM=1FF8D66526CD37F7AF3CCEC2A0F2EFAB37C9399A