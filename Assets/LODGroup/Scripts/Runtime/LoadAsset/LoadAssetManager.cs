namespace Chess.LODGroupIJob.LoadAsset
{
    public class LoadAssetManager<T>
    {
        static LoadAssetManager<T> _Instance;
        public static LoadAssetManager<T> Instance
        {
            get
            {
                if(_Instance == null)
                {
                    _Instance = new LoadAssetManager<T>();
                }
                return _Instance;
            }
        }
        public T loadAsset;
    }
}