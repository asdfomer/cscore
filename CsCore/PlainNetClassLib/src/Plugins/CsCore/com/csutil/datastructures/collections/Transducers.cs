using System;
using System.Collections.Generic;

namespace com.csutil {

    /// <summary> 
    /// Related sources:
    /// - https://jrsinclair.com/articles/2019/magical-mystical-js-transducers/
    /// </summary> 
    public static class Transducers {

        public delegate A Reducer<B, A>(A x, B t);

        /// <summary> 
        /// Make a function that takes a reducer and returns a new reducer that 
        /// filters out some items so that the original reducer never sees them.
        /// </summary> 
        public static Func<Reducer<T, R>, Reducer<T, R>> NewFilter<T, R>(Func<T, bool> filter) {
            return (nextReducer) => {
                return (acc, item) => {
                    return filter(item) ? nextReducer(acc, item) : acc;
                };
            };
        }

        /// <summary> 
        /// Make a function that takes a reducer and returns a new reducer that 
        /// transforms every time before the original reducer gets to see it.
        /// </summary> 
        public static Func<Reducer<R, R>, Reducer<T, R>> NewMapper<T, R>(Func<T, R> mapper) {
            return (nextReducer) => {
                return (acc, item) => {
                    return nextReducer(acc, mapper(item));
                };
            };
        }


        public static R Reduce<T, R>(this Reducer<T, R> self, IEnumerable<T> list, R seed) {
            return list.Reduce(seed, (result, elem) => self(result, elem));
        }

    }

}