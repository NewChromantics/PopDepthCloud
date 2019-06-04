using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MakeDumbTriangleMesh : MonoBehaviour
{
	[Range(1, 5000)]
	public int TriangleCountThousand = 100;
	public int TriangleCount { get { return TriangleCountThousand * 1000; } }

	void OnEnable()
	{
		var mf = GetComponent<MeshFilter>();
		//	make a new one on enable
		Mesh ExistingMesh = null;
		var NewMesh = DoMakeDumbTriangleMesh(TriangleCount,ref ExistingMesh);
		mf.sharedMesh = NewMesh;

	}
    
	public static void AddTriangleToMesh(ref List<Vector3> Positions,ref List<int> Indexes,ref List<Vector2> Uvs,Vector3 Position,int Index)
	{
		//	gr: increment z so overdraw doesn't kill performance when something tries to render the original mesh
		var pos0 = new Vector3( 0,-1,Index ) + Position;
		var pos1 = new Vector3( -1,0.5f, Index) + Position;
		var pos2 = new Vector3( 1,0.5f, Index) + Position;

		Positions.Add( pos0 );
		Uvs.Add( new Vector2(Index,0) );
		Indexes.Add( Positions.Count-1 );

		Positions.Add( pos1 );
		Uvs.Add( new Vector2(Index,1) );
		Indexes.Add( Positions.Count-1 );

		Positions.Add( pos2 );
		Uvs.Add( new Vector2(Index,2) );
		Indexes.Add( Positions.Count-1 );
	}

	static public Mesh DoMakeDumbTriangleMesh(int PointCount,ref Mesh ExistingMesh)
	{
		Mesh mesh = ExistingMesh;

		//	do we need a bigger mesh?
		if ( mesh != null )
		{
			//	already big enough
			if ( mesh.vertexCount >= PointCount )
				return mesh;
		}

		var Positions = new List<Vector3>();
		var Uvs = new List<Vector2>();
		var Indexes = new List<int>();

		{
			//	center triangle around 0,0,0
			for ( int i=0;	i<PointCount;	i++ )
			{
				AddTriangleToMesh (ref Positions, ref Indexes, ref Uvs, Vector3.zero, i);
			}
		}

		if ( mesh == null )
		{
			ExistingMesh = new Mesh();
			mesh = ExistingMesh;
		}

		//	re-setup existing one
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
		mesh.bounds.SetMinMax( Vector3.zero, Vector3.one );
		mesh.SetVertices( Positions );
		mesh.SetUVs( 0, Uvs );
		mesh.SetIndices( Indexes.ToArray(), MeshTopology.Triangles, 0 );
		Debug.Log ("New dumb mesh; " + Indexes.Count + " indexes, " + PointCount+ " input positions, " + (Indexes.Count / 3) + " triangles");

		mesh.name = "DumbTriangleMesh x" + PointCount;

		return mesh;
	}
}
