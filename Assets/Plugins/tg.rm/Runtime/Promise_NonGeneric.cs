using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Specifies the state of a promise.
/// </summary>
public enum PromiseState
{
    Pending,    // The promise is in-flight.
    Rejected,   // The promise has been rejected.
    Resolved    // The promise has been resolved.
};

/// <summary>
/// Implements a non-generic C# promise, this is a promise that simply resolves without delivering a value.
/// https://developer.mozilla.org/en/docs/Web/JavaScript/Reference/Global_Objects/Promise
/// </summary>
public class Promise
{
    /// <summary>
    /// The exception when the promise is rejected.
    /// </summary>
    private Exception rejectionException;

    private object resolveValue;

    /// <summary>
    /// Represents a handler invoked when the promise is resolved.
    /// </summary>
    public struct ResolveHandler
    {
        /// <summary>
        /// Callback fn.
        /// </summary>
        public Action<object> callback;
    }

    /// <summary>
    /// Completed handlers that accept no value.
    /// </summary>
    private List<ResolveHandler> resolveHandlers;

    /// <summary>
    /// Tracks the current state of the promise.
    /// </summary>
    public virtual PromiseState CurState { get; private set; }

    public Promise()
    {
        this.CurState = PromiseState.Pending;
    }

    public Promise(Action<Action<object>> resolver)
    {
        this.CurState = PromiseState.Pending;

        try
        {
            resolver(Resolve);
        }
        catch (Exception ex)
        {
            this.CurState = PromiseState.Rejected;
            rejectionException = ex;
            resolver(Resolve);
            Debug.LogError(ex.ToString());
        }
    }

    private Promise(PromiseState initialState)
    {
        CurState = initialState;
    }

    /// <summary>
    /// Add a resolve handler for this promise.
    /// </summary>
    private void AddResolveHandler(Action<object> onResolved)
    {
        if (resolveHandlers == null)
        {
            resolveHandlers = new List<ResolveHandler>();
        }

        resolveHandlers.Add(new ResolveHandler
        {
            callback = onResolved
        });
    }

    /// <summary>
    /// Helper function clear out all handlers after resolution or rejection.
    /// </summary>
    private void ClearHandlers()
    {
        if (resolveHandlers != null)
        {
            resolveHandlers.Clear();
            resolveHandlers = null;
        }
    }

    /// <summary>
    /// Invoke all resolve handlers.
    /// </summary>
    private void InvokeResolveHandlers(object value)
    {
        if (resolveHandlers != null)
        {
            for (int i = 0, maxI = resolveHandlers.Count; i < maxI; i++)
            {
                resolveHandlers[i].callback(value);
            }
        }

        ClearHandlers();
    }

    /// <summary>
    /// Resolve the promise with a particular value.
    /// </summary>
    public virtual void Resolve(object value = null)
    {
        if (CurState != PromiseState.Pending)
        {
            UnityEngine.Debug.LogWarning(
                "Attempt to resolve a promise that is already in state: " + CurState
                + ", a promise can only be resolved when it is still in state: "
                + PromiseState.Pending
            );
        }

        resolveValue = value;
        CurState = PromiseState.Resolved;

        InvokeResolveHandlers(value);
    }

    /// <summary>
    /// Add a resolved callback, a rejected callback and a progress callback.
    /// The resolved callback chains a non-value promise.
    /// </summary>
    public virtual Promise ContinueWith(Func<object, Promise> onResolved)
    {
        if (CurState == PromiseState.Resolved || CurState == PromiseState.Rejected)
        {
            try
            {
                var onResRetPromise = onResolved(resolveValue);
                if (onResRetPromise == null)
                {
#if LOG_NORMAL_ENABLE || UNITY_EDITOR
                    // 非正式环境中移除保护暴露问题
                    Debug.LogError("Promise.ContinueWith 返回 N/A ，这可能导致后续的 NPE 问题。请结合后续报错并调查以下内容确认问题原因。\n" +
                        "#1 Promise.ContinueWith 是否所有条件路径都返回了有意义的 Promise；\n" +
                        "#2 返回 Promise 的调用堆栈上是否出现了未处理的异常； \n" +
                        "#3 Lua 中使用 Promise.ContinueWith 没有 return 操作。");
#else
                    onResRetPromise = Promise.Resolved();
#endif
                }
                return onResRetPromise;
            }
            catch (Exception ex)
            {
                this.CurState = PromiseState.Rejected;
                rejectionException = ex;
                Promise promise = new Promise(PromiseState.Resolved);
                Debug.LogError(ex.ToString());
                return promise;
            }
        }
        else
        {
            var resultPromise = new Promise();

            Action<object> resolveHandler;
            if (onResolved != null)
            {
                resolveHandler = v =>
                {
                    try
                    {
                        var onResRetPromise = onResolved(v);
                        if (onResRetPromise == null)
                        {
#if LOG_NORMAL_ENABLE || UNITY_EDITOR
                            // 非正式环境中移除保护暴露问题
                            Debug.LogError("Promise.ContinueWith 返回 N/A ，这可能导致后续的 NPE 问题。请结合后续报错并调查以下内容确认问题原因。\n" +
                                "#1 Promise.ContinueWith 是否所有条件路径都返回了有意义的 Promise；\n" +
                                "#2 返回 Promise 的调用堆栈上是否出现了未处理的异常； \n" +
                                "#3 Lua 中使用 Promise.ContinueWith 没有 return 操作。");
#else
                            onResRetPromise = Promise.Resolved();
#endif
                        }
                        onResRetPromise.Then(chainedValue => resultPromise.Resolve(chainedValue));
                    }
                    catch (Exception ex)
                    {
                        this.CurState = PromiseState.Rejected;
                        rejectionException = ex;
                        Promise promise = new Promise(PromiseState.Resolved);
                        promise.Then(chainedValue => resultPromise.Resolve(chainedValue));
                        Debug.LogError(ex.ToString());
                    }
                };
            }
            else
            {
                resolveHandler = resultPromise.Resolve;
            }

            AddResolveHandler(resolveHandler);

            return resultPromise;
        }
    }

    /// <summary>
    /// Add a resolved callback, a rejected callback and a progress callback.
    /// </summary>
    public virtual Promise Then(Action<object> onResolved)
    {
        if (CurState == PromiseState.Resolved || CurState == PromiseState.Rejected)
        {
            try
            {
                onResolved(resolveValue);
            }
            catch (Exception ex)
            {
                this.CurState = PromiseState.Rejected;
                rejectionException = ex;
                Debug.LogError(ex.ToString());
            }
            return this;
        }
        else
        {
            var resultPromise = new Promise();

            Action<object> resolveHandler;
            if (onResolved != null)
            {
                resolveHandler = v =>
                {
                    try
                    {
                        onResolved(v);
                    }
                    catch (Exception ex)
                    {
                        this.CurState = PromiseState.Rejected;
                        rejectionException = ex;
                        Debug.LogError(ex.ToString());
                    }
                    resultPromise.Resolve(v);
                };
            }
            else
            {
                resolveHandler = resultPromise.Resolve;
            }

            AddResolveHandler(resolveHandler);

            return resultPromise;
        }
    }

    /// <summary>
    /// Returns a promise that resolves when all of the promises in the enumerable argument have resolved.
    /// Returns a promise of a collection of the resolved results.
    /// </summary>
    public static Promise All(params Promise[] promisesArray)
    {
        return AllInternal(promisesArray);
    }

    /// <summary>
    /// Returns a promise that resolves when all of the promises in the enumerable argument have resolved.
    /// Returns a promise of a collection of the resolved results.
    /// </summary>
    public static Promise All(IEnumerable<Promise> promises)
    {
        return AllInternal(promises.ToArray());
    }

    /// <summary>
    /// Returns a promise that resolves when all of the promises in the enumerable argument have resolved.
    /// Returns a promise of a collection of the resolved results.
    /// </summary>
    private static Promise AllInternal(Promise[] promisesArray)
    {
        if (promisesArray.Length == 0)
        {
            return new Promise(PromiseState.Resolved);
        }

        var remainingCount = promisesArray.Length;
        var resultPromise = new Promise();
        Promise promise;

        for (int i = 0; i != promisesArray.Length; ++i)
        {
            int localIndex = i;

            promise = promisesArray[localIndex];
            if (promise == null)
            {
                promise = new Promise(PromiseState.Resolved);
                promisesArray[i] = promise;
            }
            promise.Then(obj =>
            {
                --remainingCount;
                if (remainingCount <= 0 && resultPromise.CurState == PromiseState.Pending)
                {
                    // This will never happen if any of the promises errorred.
                    resultPromise.Resolve(promisesArray.Select(x => x.resolveValue).ToArray());
                }
            });
        }

        return resultPromise;
    }

    public static Promise Resolved()
    {
        return new Promise(PromiseState.Resolved);
    }
}
