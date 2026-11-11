using UnityEngine;

namespace Winterbourne.Pass
{
    // Original Foley: damped wood, a brass ratchet, the latch clearing the keep,
    // followed by a short hinge creak and a subdued stop at the end of travel.
    internal static class PassChestSound
    {
        internal static AudioClip Create(int sound)
        {
            const int rate=22050;
            float length=sound>=3 ? .48f : sound==2 ? .21f : .095f;
            float[] samples=new float[Mathf.CeilToInt(rate*length)];
            var noise=new System.Random(3523+sound);
            float previous=0,phase=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate;
                float white=(float)(noise.NextDouble()*2-1);
                float high=white-previous;previous=white;
                float value;
                if(sound==0)
                    value=.35f*Tone(t,182)*Mathf.Exp(-t*57)+.20f*high*Mathf.Exp(-t*136);
                else if(sound==1 || sound==2)
                {
                    float f=sound==1 ? 1260 : 710;
                    value=(.22f*Tone(t,f)+.12f*Tone(t,f*2.431f)+.14f*high*Mathf.Exp(-t*102))*Mathf.Exp(-t*(sound==1?71:32));
                }
                else
                {
                    float s=t/length;
                    phase+=(sound==3?103:157)*(1+.16f*Mathf.Sin(t*22))*(1-.34f*s)/rate;
                    float rub=.12f*(Mathf.Sin(phase*Mathf.PI*2)+.4f*Mathf.Sin(phase*Mathf.PI*6))+.045f*white;
                    value=rub*Mathf.Sin(s*Mathf.PI)*(.5f+.5f*Mathf.Sin(s*14));
                    if(t>.37f)value+=.24f*Tone(t-.37f,140)*Mathf.Exp(-(t-.37f)*90);
                }
                samples[i]=value*Mathf.Clamp01(t*1800)*Mathf.Clamp01((length-t)*140);
            }
            var clip=AudioClip.Create("Fitted luggage mechanism "+sound,samples.Length,1,rate,false);
            clip.SetData(samples,0);
            return clip;
        }
        static float Tone(float t,float f)=>Mathf.Sin(t*f*2*Mathf.PI);
    }
}
