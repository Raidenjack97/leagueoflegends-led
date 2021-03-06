﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedDashboard.Modules.FourierAudioLED
{
    class FourierAudioLED : LEDModule
    {
        AudioEngine audio;

        const float BASS_MAX_FREQ = 225;
        /*const float BASS_TOP_LIMIT = 1750000;
        const float MID_TOP_LIMIT = 450000;*/
        const float BASS_TOP_LIMIT = 10; //5;
        const float MID_TOP_LIMIT = 2; //3;
        const float LED_COLOR_MIX_SPREAD = 5;
        const float FADE_TO_BLACK_FACTOR = 0.3f;

        const double FFT_MAXFREQ = AudioEngine.RATE / 2;

        double FFT_SPACING_HZ;

        int ledCount;

        int lastIndex;
        int[] freqArray;

        Led[] leds;

        public event LEDModule.FrameReadyHandler NewFrameReady;

        public static LEDModule Create(int ledCount)
        {
            FourierAudioLED module = new FourierAudioLED();
            module.Initialize(ledCount);
            return module;
        }
        public void Initialize(int ledCount)
        {
            audio = new AudioEngine();
            audio.Start();
            audio.NewData += OnNewAudioData;
            this.ledCount = ledCount;
            FFT_SPACING_HZ = FFT_MAXFREQ / ledCount;

            this.lastIndex = (int)(2500 / FFT_SPACING_HZ);
            this.freqArray = new int[this.lastIndex];
            for (int i = 0; i < freqArray.Length; i++)
            {
                this.freqArray[i] = (int)Math.Floor(Utils.Scale(FFTUtil.GetFreqForIndex(i, FFT_SPACING_HZ), 0, 2500, 0, this.ledCount/2 + 1));
            }
            leds = new Led[ledCount];
            for(int i = 0; i < ledCount; i++)
            {
                leds[i] = new Led();
            }
        }

        public Led[] DoFrame() // TODO: Latency issues...
        {
            leds.FadeToBlackAllLeds(0.3f); // 0.3f * delta que es 20 ms se supone
            if (!newAudioAvailable) return this.leds;
            newAudioAvailable = false;
            var y_in = FFTUtil.ProcessAudio(audio);
            if (y_in.Length < this.lastIndex) return this.leds;
            var y = new double[this.lastIndex];
            Array.Copy(y_in, 0, y, 0, this.lastIndex);
            y = y.Select(x => x < 0 ? 0 : x).ToArray();
            double[] y_scaled = new double[this.lastIndex];

            for (int i = 0; i < this.lastIndex; i++)
            {
                var freq = FFTUtil.GetFreqForIndex(i, FFT_SPACING_HZ);
                var val = y[i];
                double scaled;
                if (freq <= BASS_MAX_FREQ)
                {
                    scaled = Utils.Scale(val, 0, BASS_TOP_LIMIT, 0, 1);
                }
                else
                {
                    scaled = Utils.Scale(val, 0, MID_TOP_LIMIT, 0, 1);
                }
                y_scaled[i] = scaled;
            }

            HSVColor[] yColors = new HSVColor[this.lastIndex];
            for (int i = 0; i < this.lastIndex; i++)
            {
                yColors[i] = UtilsLED.ValueToHSV(this.ledCount / 2, this.freqArray[i], y_scaled[i]);
            }

            for (int i = 0; i < this.lastIndex; i++)
            {
                if (this.freqArray[i] == this.ledCount / 2) break;
                leds.AddSymmetricColorAroundLeds(this.freqArray[i], yColors[i]);
            }

            return this.leds;
        }


        bool newAudioAvailable = false;

        void OnNewAudioData()
        {
            DoFrame();
            NewFrameReady?.Invoke(this, this.leds, LightingMode.Line);
            newAudioAvailable = true;
        }

        public void Dispose()
        {
            audio.Stop();
        }
    }
}
