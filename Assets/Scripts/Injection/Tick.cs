using System;
using UnityEngine;

namespace Injection
{
    public class Tick : MonoBehaviour
    {
        public Action OnTick;
        public Action OnTick5Seconds;

        private float _timer5Seconds = 0f;
        private const float Interval5Seconds = 5f;

        void Update()
        {
            OnTick?.Invoke();

            _timer5Seconds += Time.deltaTime;
            if (_timer5Seconds >= Interval5Seconds)
            {
                OnTick5Seconds?.Invoke();
                _timer5Seconds -= Interval5Seconds; 
            }
        }
    }
}