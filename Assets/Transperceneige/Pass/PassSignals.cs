using UnityEngine;

namespace Winterbourne.Pass
{
    static class PassSignals
    {
        public static AudioClip Make(bool bell)
        {
            const int rate=22050;
            float length=bell ? 4.2f : 2.18f;
            var wave=new float[(int)(rate*length)];
            for(int i=0;i<wave.Length;i++){
                float t=(float)i/rate, sample;
                if(bell){
                    float hit=t%1.05f;
                    sample=Mathf.Exp(-hit*4)*(Osc(739,t)+.42f*Osc(1731,t)+.18f*Osc(2768,t));
                }else{
                    float envelope=Mathf.Clamp01(t/.13f)*Mathf.Clamp01((length-t)/.2f);
                    sample=envelope*(Osc(697,t)+.60f*Osc(936,t)+.23f*Osc(1402,t))*
                        (.86f+.14f*Mathf.Sin(t*26));
                }
                wave[i]=sample*.29f*Mathf.Clamp01((length-t)*24);
            }
            var clip=AudioClip.Create(bell ? "Bridge warning bell" : "Departure whistle",wave.Length,1,rate,false);
            clip.SetData(wave,0);
            return clip;
        }
        static float Osc(float hz,float t)=>Mathf.Sin(t*hz*2*Mathf.PI);
    }
}
