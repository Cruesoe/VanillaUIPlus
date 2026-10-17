using System.Collections;
using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>Tints the tiles of the open world search's results. Added to the surface layer by a patch.</summary>
[StaticConstructorOnStartup]
public class WorldDrawLayer_SearchHighlight : WorldDrawLayer
{
    private const int RebuildInterval = 15;
    private static Material? material;
    private readonly List<Vector3> verts = new List<Vector3>();
    private int drawnVersion = -1;
    private int builtFrame = -100;

    private static Material Material => material ??= MaterialPool.MatFrom(new MaterialRequest(BaseContent.WhiteTex,
        ShaderDatabase.WorldOverlayTransparent, new Color(0.3f, 0.85f, 1f, 0.35f)) { renderQueue = 3560 });

    public override bool Visible => base.Visible && WorldSearchHighlight.Showing;

    public override bool VisibleInBackground => false;

    public override bool ShouldRegenerate => base.ShouldRegenerate
        || (WorldSearchHighlight.Version != drawnVersion && Time.frameCount - builtFrame >= RebuildInterval);

    public override IEnumerable Regenerate()
    {
        foreach (object item in base.Regenerate())
        {
            yield return item;
        }

        drawnVersion = WorldSearchHighlight.Version;
        builtFrame = Time.frameCount;
        foreach (PlanetTile tile in WorldSearchHighlight.Tiles)
        {
            if (tile.Layer != planetLayer)
            {
                continue;
            }

            LayerSubMesh subMesh = GetSubMesh(Material);
            Find.WorldGrid.GetTileVertices(tile, verts);
            int start = subMesh.verts.Count;
            for (int i = 0; i < verts.Count; i++)
            {
                subMesh.verts.Add(verts[i] + verts[i].normalized * 0.015f);
                subMesh.uvs.Add((GenGeo.RegularPolygonVertexPosition(verts.Count, i) + Vector2.one) / 2f);
                if (i < verts.Count - 2)
                {
                    subMesh.tris.Add(start + i + 2);
                    subMesh.tris.Add(start + i + 1);
                    subMesh.tris.Add(start);
                }
            }
        }

        FinalizeMesh(MeshParts.All);
    }
}
