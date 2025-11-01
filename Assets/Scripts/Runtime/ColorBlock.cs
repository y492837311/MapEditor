using UnityEngine;

namespace MapEditor
{
    [System.Serializable]
    public struct ColorBlock
    {
        public int id;
        public Color color;
        public string name;
        public RectInt bounds;
        
        public ColorBlock(int id, Color color, string name)
        {
            this.id = id;
            this.color = color;
            this.name = name;
            this.bounds = new RectInt();
        }
        
        public override string ToString()
        {
            return $"{name} (ID: {id}, Color: {color})";
        }
    }
}