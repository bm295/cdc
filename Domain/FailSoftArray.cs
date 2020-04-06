using System;
using System.Collections.Generic;
using System.Text;

namespace Domain
{
    public class FailSoftArray
    {
        private int[] arr;
        public int Length;
        public bool ErrFlag;

        public FailSoftArray(int size)
        {
            arr = new int[size];
            Length = size;
        }

        public int this[int index]
        {
            get
            {
                if (WithinRange(index))
                {
                    ErrFlag = false;
                    return arr[index];
                }
                else
                {
                    ErrFlag = true;
                    return 0;
                }
            }
            set
            {
                if (WithinRange(index))
                {
                    arr[index] = value;
                    ErrFlag = false;
                }
                else
                {
                    ErrFlag = true;
                }
            }
        }

        private bool WithinRange(int index)
        {
            if (index >= 0 && index < Length)
            {
                return true;
            }

            return false;
        }
    }
}
