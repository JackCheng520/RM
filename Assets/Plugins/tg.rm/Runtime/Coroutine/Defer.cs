namespace TG.AssetBundleRM
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Process all defer event through CoroutineManager script.
    /// </summary>
    public static class Defer
    {
        /// <summary>
        /// Wait the specified frame count.
        /// </summary>
        /// <param name="frameCount">The frame count.</param>
        /// <param name="action">The action.</param>
        /// <returns>.</returns>
        public static CoroutineHandle Frames(int frameCount, Action action)
        {
            return CoroutineManager.RunCoroutine(DeferFramesInternal(frameCount, action));
        }

        /// <summary>
        /// Wait the specified seconds.
        /// </summary>
        /// <param name="seconds">The seconds.</param>
        /// <param name="action">The action.</param>
        /// <returns>.</returns>
        public static CoroutineHandle Seconds(float seconds, Action action)
        {
            return CoroutineManager.RunCoroutine(DeferSecondsInternal(seconds, action));
        }

        /// <summary>
        /// The EndOfFrame.
        /// </summary>
        /// <param name="action">The action<see cref="Action"/>.</param>
        /// <returns>The <see cref="CoroutineHandle"/>.</returns>
        public static CoroutineHandle EndOfFrame(Action action)
        {
            return CoroutineManager.RunCoroutine(_EndOfFrame(action));
        }

        /// <summary>
        /// The SecondsReal.
        /// </summary>
        /// <param name="seconds">The seconds<see cref="float"/>.</param>
        /// <param name="action">The action<see cref="Action"/>.</param>
        public static void SecondsReal(float seconds, Action action)
        {
            CoroutineManager.Instance.StartCoroutine(DeferSecondsRealInternal(seconds, action));
        }

        /// <summary>
        /// The DeferSecondsInternal.
        /// </summary>
        /// <param name="seconds">The seconds<see cref="float"/>.</param>
        /// <param name="action">The action<see cref="Action"/>.</param>
        /// <returns>The <see cref="IEnumerator{float}"/>.</returns>
        private static IEnumerator<float> DeferSecondsInternal(float seconds, Action action)
        {
            yield return CoroutineManager.WaitForSeconds(seconds);
            action();
        }

        /// <summary>
        /// The DeferSecondsRealInternal.
        /// </summary>
        /// <param name="seconds">The seconds<see cref="float"/>.</param>
        /// <param name="action">The action<see cref="Action"/>.</param>
        /// <returns>The <see cref="IEnumerator"/>.</returns>
        private static IEnumerator DeferSecondsRealInternal(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }

        /// <summary>
        /// The DeferFramesInternal.
        /// </summary>
        /// <param name="frameCount">The frameCount<see cref="int"/>.</param>
        /// <param name="action">The action<see cref="Action"/>.</param>
        /// <returns>The <see cref="IEnumerator{float}"/>.</returns>
        private static IEnumerator<float> DeferFramesInternal(int frameCount, Action action)
        {
            for (int i = 0; i < frameCount; i++)
                yield return CoroutineManager.WaitForOneFrame;
            action();
        }

        /// <summary>
        /// The _EndOfFrame.
        /// </summary>
        /// <param name="action">The action<see cref="System.Action"/>.</param>
        /// <returns>The <see cref="IEnumerator{float}"/>.</returns>
        private static IEnumerator<float> _EndOfFrame(System.Action action)
        {
            yield return CoroutineManager.WaitForOneFrame;
            action?.Invoke();
        }

        /// <summary>
        /// Runs the coroutine.
        /// </summary>
        /// <param name="coroutine">The coroutine.</param>
        /// <returns>.</returns>
        public static CoroutineHandle RunCoroutine(IEnumerator<float> coroutine)
        {
            return CoroutineManager.RunCoroutine(coroutine); //(coroutine);
        }

        /// <summary>
        /// Waits the until done. （ using yield return method ).
        /// </summary>
        /// <param name="coroutine">The coroutine.</param>
        /// <returns>.</returns>
        public static float WaitUntilDone(IEnumerator<float> coroutine)
        {
            return CoroutineManager.WaitUntilDone(CoroutineManager.RunCoroutine(coroutine));
        }

        /// <summary>
        /// Waits the until done. ( using yield return method ).
        /// </summary>
        /// <param name="www">The WWW.</param>
        /// <returns>.</returns>
        public static float WaitUntilDone(WWW www)
        {
            return CoroutineManager.WaitUntilDone(www);
        }

        /// <summary>
        /// Waits for seconds.
        /// </summary>
        /// <param name="seconds">The seconds.</param>
        /// <returns>.</returns>
        public static float WaitForSeconds(float seconds)
        {
            return CoroutineManager.WaitForSeconds(seconds);
        }

        /// <summary>
        /// Stops the coroutine.
        /// </summary>
        /// <param name="coroutineHandler">The coroutine handler.</param>
        /// <returns>.</returns>
        public static bool StopCoroutine(CoroutineHandle coroutineHandler)
        {
            return CoroutineManager.KillCoroutines(coroutineHandler) != 0;
        }

        /// <summary>
        /// Gets the WaitForOneFrame.
        /// </summary>
        public static float WaitForOneFrame
        {
            get { return CoroutineManager.WaitForOneFrame; }
        }
    }
}
