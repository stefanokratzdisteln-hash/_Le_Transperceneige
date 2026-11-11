using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Winterbourne.Editor
{
    internal static partial class PassExtractionReview
    {
        const int FilmRate=24;
        static string filmDir;
        static int recordedFrames;
        static double filmStart;

        static void StartChestFilm(string name)
        {
            var directory=FleetImport.Argument("-chestReviewPhotos");
            if(directory==null || !gameFrame) return;
            filmDir=Path.Combine(directory,name+"Frames");
            Directory.CreateDirectory(filmDir);
            recordedFrames=0;
            filmStart=Time.realtimeSinceStartupAsDouble;
            EditorApplication.update+=CaptureChestFilmFrame;
        }

        static void CaptureChestFilmFrame()
        {
            if(string.IsNullOrEmpty(filmDir)) return;
            int next=(int)((Time.realtimeSinceStartupAsDouble-filmStart)*FilmRate);
            if(recordedFrames>next)return;
            // Frame duration follows elapsed play time even when rendering the
            // full Game view takes longer than one output frame.
            string path=Path.Combine(filmDir,recordedFrames.ToString("D4")+".jpg");
            WriteGameFrame(path,false);
            while(++recordedFrames<=next)
                File.Copy(path,Path.Combine(filmDir,recordedFrames.ToString("D4")+".jpg"),true);
        }

        static void StopChestFilm()
        {
            if(filmDir==null)return;
            EditorApplication.update-=CaptureChestFilmFrame;
            Debug.Log($"CABINET FILM: {filmDir}, {recordedFrames} frames at {FilmRate} fps");
            filmDir=null;
        }
    }
}
