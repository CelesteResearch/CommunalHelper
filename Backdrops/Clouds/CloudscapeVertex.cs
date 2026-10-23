using System.Numerics;
using System.Runtime.InteropServices;
using Celeste.Graphics.Rendering.Vertices;

namespace Celeste.Mods.CommunalHelper.Backdrops.Clouds
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CloudscapeVertex : ISourceVertexType
    {
        public Vector2 Polar, Texture;
        public short CloudIndex, RingIndex;

        private static readonly SourceVertexDeclaration Declaration = new(
            new SourceVertexElement(0, SourceVertexElementFormat.Vector2, SourceVertexElementUsage.TextureCoordinate,
                0),
            new SourceVertexElement(8, SourceVertexElementFormat.Vector2, SourceVertexElementUsage.TextureCoordinate,
                1),
            new SourceVertexElement(16, SourceVertexElementFormat.Short2, SourceVertexElementUsage.TextureCoordinate,
                2));

        public SourceVertexDeclaration VertexDeclaration => Declaration;

        public CloudscapeVertex(float angle, float distance, Vector2 texture, short index, short ring)
        {
            Polar = new(angle, distance);
            Texture = texture;
            CloudIndex = index;
            RingIndex = ring;
        }
    }
}
