Shader "New Chromantics/Mesh To Cloud"
{
	Properties
	{
		DepthTexture("DepthTexture", 2D)  = "black" {}
		DepthMin("DepthMin", Range(0,100) ) = 0
		DepthMax("DepthMax", Range(0,100) ) = 20
		TriangleScale("TriangleScale", Range(0.01,2) ) = 0.1
		[Toggle]Billboard("Billboard", Range(0,1) ) = 1
		ForceAtlasIndex("ForceAtlasIndex", Range(-1,3) ) = -1
		ClipRadius("ClipRadius", Range(0,1) ) = 1
		AtlasSectionScale("AtlasSectionScale", Range(0.5,3) ) = 1
		RandomAtlas("RandomAtlas", Range(0,1) ) = 0

		ColourMult("ColourMult", Range(1,2) ) = 1
		ColourSquaredFactor("ColourSquaredFactor", Range(0,1) ) = 0

		Debug_ClipRadius("Debug_ClipRadius", Range(0,1) ) = 0
		Debug_TriangleUv("Debug_TriangleUv", Range(0,1) ) = 0

		ClipInsideCameraRadius("ClipInsideCameraRadius", Range(0,1) ) = 0.5
		MinDistanceFromCamera("MinDistanceFromCamera", Range(0,1) ) = 0

		MaxBeesThousand("MaxBeesThousand", Range(1,10) ) = 10

		MinScreenSize("MinScreenSize", Range(0,0.1) ) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" }
		LOD 100
		Cull Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 LocalPosition : POSITION;
				float2 TriangleIndex_CornerIndex : TEXCOORD0;
			};

			struct v2f
			{
				float4 ClipPos : SV_POSITION;
				float3 Colour : TEXCOORD0;
				float2 LocalPos : TEXCOORD1;
				float3 WorldPos : TEXCOORD2;
			};

			sampler2D DepthTexture;
			float4 DepthTexture_TexelSize;
			float DepthMin;
			float DepthMax;

			float TriangleScale;
			float MinScreenSize;
			float Billboard;
			float AtlasSectionScale;

			int ForceAtlasIndex;
			float MaxBeesThousand;

			float ClipRadius;
			float Debug_ClipRadius;
			float Debug_TriangleUv;
			float RandomAtlas;

			float ColourMult;
			float ColourSquaredFactor;
	
			float4x4 CameraLocalToWorldMatrix;
			float ClipInsideCameraRadius;		//	hide
			float MinDistanceFromCamera;		//	reposition


			#define DEBUG_CLIPRADIUS	( Debug_ClipRadius > 0.5f )
			#define DEBUG_TRIANGLEUV	( Debug_TriangleUv > 0.5f )

			#define ENABLE_RANDOMATLAS	( RandomAtlas > 0.5f )
			#define ENABLE_BILLBOARD	( Billboard > 0.5f )
			
			
						
			float3 NormalToRedGreen(float Normal)
			{
				if ( Normal < 0.5 )
				{
					Normal = Normal / 0.5;
					return float3( 1, Normal, 0 );
				}
				else if ( Normal <= 1 )
				{
					Normal = (Normal-0.5) / 0.5;
					return float3( 1-Normal, 1, 0 );
				}
				
				//	>1
				return float3( 0,0,1 );
			}



			float GetScreenCorrectionScalar(float3 WorldPos,float3 LocalOffset)
			{
				float4 ViewCenter = mul( UNITY_MATRIX_V, float4(WorldPos,1) );
				float4 ViewOffset = ViewCenter + float4( LocalOffset, 0 );

				float4 ScreenCenter4 = mul( UNITY_MATRIX_P, ViewCenter );
				float4 ScreenOffset4 = mul( UNITY_MATRIX_P, ViewOffset );

				float2 ScreenCenter = ScreenCenter4.xy / ScreenCenter4.w;
				float2 ScreenOffset = ScreenOffset4.xy / ScreenOffset4.w;

				//	this should be (half) width in screenspace, so if its too small, we HOPE we can correct the view pos
				//	(technically its not, but should only affect when far away)
				float ScreenSize = length( ScreenCenter - ScreenOffset );
				if ( ScreenSize > MinScreenSize )
					return 1;

				return MinScreenSize / ScreenSize;					
			}
			
			#define Width	100
			#define Height	100
			void IndexToXy(int Index,out int2 xy,out float2 uv)
			{
				int x = Index % Width;
				int y = Index / Width;
				xy = int2(x,y);
				uv.x = x / (float)Width;
				uv.y = y / (float)Height;
			}
			
			float3 GetWorldPos(int Index,out float DepthNormal)
			{
				float2 uv;
				int2 xy;
				IndexToXy( Index, xy, uv );
				//float2 uv = float2( x, y ) * DepthTexture_TexelSize.xy;
				
				DepthNormal = tex2Dlod( DepthTexture, float4( uv, 0, 0 ) ).x;
				float Depth = lerp( DepthMin, DepthMax, DepthNormal );
				float3 WorldPos = float3( xy.x, xy.y, Depth );
				return WorldPos;
			}
			
			
			

			v2f vert (appdata v)
			{
				v2f o;

				float4 LocalPos = v.LocalPosition;

				int BeeIndex = v.TriangleIndex_CornerIndex.x;
				float DepthNormal;
				float3 WorldPos = GetWorldPos(BeeIndex,DepthNormal);
				
				float ScalarCorrection = GetScreenCorrectionScalar( WorldPos, LocalPos * TriangleScale );

				//	gr: why am I using CameraLocalToWorldMatrix?
				//float3 CameraPos = mul( CameraLocalToWorldMatrix, float4(0,0,0,1) );
				float3 CameraPos = _WorldSpaceCameraPos;
				float3 DeltaToCamera = WorldPos - CameraPos;

				//	force distance to be away from camera
				float DistanceToCamera = length( DeltaToCamera );
				DistanceToCamera = max( DistanceToCamera, MinDistanceFromCamera );
				DeltaToCamera = normalize( DeltaToCamera ) * DistanceToCamera;
				WorldPos = CameraPos + DeltaToCamera;

				if ( ENABLE_BILLBOARD )
				{
					//	+ offset here is billboarding in view space
					float3 ViewPos = mul( UNITY_MATRIX_V, float4(WorldPos,1) ) + ( LocalPos * TriangleScale * ScalarCorrection);
					o.ClipPos = mul( UNITY_MATRIX_P, float4(ViewPos,1) );
				}
				else
				{
					WorldPos += LocalPos * TriangleScale;
					o.ClipPos = UnityWorldToClipPos( float4(WorldPos,1) );
				}

				if ( BeeIndex > MaxBeesThousand*1000.0f )
				{
					o.ClipPos = 0;
				}

				if ( DistanceToCamera < ClipInsideCameraRadius )
				{
					o.ClipPos = 0;
				}


				float IndexNorm = BeeIndex / 10000.0f;
				o.Colour = NormalToRedGreen( IndexNorm );
				o.Colour = float3(DepthNormal,DepthNormal,DepthNormal);
				
				o.LocalPos = LocalPos;
				o.WorldPos = WorldPos;

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				if ( length( i.LocalPos ) > ClipRadius )
				{
					if ( DEBUG_CLIPRADIUS )
						return float4(1,0,0,1);
					discard;
				}
				
				return float4(i.Colour,1);
			}
			ENDCG
		}
	}
}
