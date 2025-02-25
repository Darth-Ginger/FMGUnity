

namespace GimmeDOTSGeometry
{
    public interface IBinaryPersistable : ISavable
    {

        public void SaveAsBinary(string filePath);
        public void LoadFromBinary(string filePath);
        public byte[] SerializeBinary();
        public void DeserializeBinary(byte[] data);

    }
}