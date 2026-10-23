using Celeste.Graphics.Rendering.Vertices;
using System.Collections.Generic;
using Celeste.Core.Rendering;
using Celeste.Graphics.Rendering.Devices;

namespace Celeste.Mods.CommunalHelper.Backdrops.Clouds
{
    internal sealed class CloudscapeMesh
    {
        private readonly List<CloudscapeVertex> vertices = [];
        private readonly List<int> indices = [];
        private CloudscapeVertex[] bakedVertices;
        private int[] bakedIndices;
        public int VertexCount => bakedVertices?.Length ?? vertices.Count;

        public void AddVertices(CloudscapeVertex a, CloudscapeVertex b)
        {
            vertices.Add(a);
            vertices.Add(b);
        }

        public void AddTriangle(int a, int b, int c)
        {
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }

        public void Bake()
        {
            bakedVertices = vertices.ToArray();
            bakedIndices = indices.ToArray();
            vertices.Clear();
            vertices.TrimExcess();
            indices.Clear();
            indices.TrimExcess();
        }

        public void Draw(SourceGraphicsDevice device)
        {
            if (VertexCount == 0) return;
            device.DrawUserIndexedPrimitives(SourcePrimitiveType.TriangleList, bakedVertices, 0, VertexCount,
                bakedIndices, 0, bakedIndices.Length / 3);
        }
    }
}
