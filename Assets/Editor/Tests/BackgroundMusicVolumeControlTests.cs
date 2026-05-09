using NUnit.Framework;
using UnityEngine;

public sealed class BackgroundMusicVolumeControlTests
{
    [Test]
    public void SetVolumeClampsToAudioRange()
    {
        var gameObject = new GameObject("music");

        try
        {
            var control = gameObject.AddComponent<BackgroundMusicVolumeControl>();

            control.SetVolume(2f);
            Assert.AreEqual(1f, control.volume);

            control.SetVolume(-1f);
            Assert.AreEqual(0f, control.volume);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ApplyVolumeUpdatesAudioSource()
    {
        var gameObject = new GameObject("music");

        try
        {
            var source = gameObject.AddComponent<AudioSource>();
            var control = gameObject.AddComponent<BackgroundMusicVolumeControl>();

            control.SetVolume(0.35f);

            Assert.AreEqual(0.35f, source.volume);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
