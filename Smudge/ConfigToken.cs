using PaintDotNet;
using PaintDotNet.Effects;
using pyrochild.effects.common;

namespace pyrochild.effects.smudge
{
    class ConfigToken : EffectConfigToken
    {
        public Surface surface;

        //these are just what were last selected in the dialog, so it can "remember" between uses
        public SmudgeBrush brush;
        public int width;
        public float strength;
        public float jitter;
        public float quality;

        public ConfigToken()
        {
            brush = null;
            width = 30;
            strength = 0.25f;
            jitter = 0f;
            quality = 1.0f;
        }

        public ConfigToken(ConfigToken toCopy)
        {
            this.surface = toCopy.surface;
            this.brush = toCopy.brush;
            this.width = toCopy.width;
            this.strength = toCopy.strength;
            this.jitter = toCopy.jitter;
            this.quality = toCopy.quality;
        }

        public override object Clone()
        {
            return new ConfigToken(this);
        }
    }
}