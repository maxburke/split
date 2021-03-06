﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Split
{
    class FreeCam
    {
        int[] mHalfWindowSize;
        Vector2 mRotation = new Vector2();
        Vector3 mPosition = new Vector3();
        Matrix mView = new Matrix();
        Quaternion mCameraQuaternion;

        public static FreeCam Instance;

        public FreeCam(int windowX, int windowY)
        {
            mHalfWindowSize = new int[] { windowX / 2, windowY / 2 };
            Mouse.SetPosition(mHalfWindowSize[0], mHalfWindowSize[1]);
            Instance = this;
        }

        void ResetMouse()
        {
            Mouse.SetPosition(mHalfWindowSize[0], mHalfWindowSize[1]);
        }

        void UpdatePosition()
        {
            KeyboardState KS = Keyboard.GetState();
            Vector3 displacement = new Vector3();

            if (KS.IsKeyDown(Keys.W))
                displacement += new Vector3(0, 0, -1);

            if (KS.IsKeyDown(Keys.S))
                displacement += new Vector3(0, 0, 1);

            if (KS.IsKeyDown(Keys.D))
                displacement += new Vector3(1, 0, 0);

            if (KS.IsKeyDown(Keys.A))
                displacement += new Vector3(-1, 0, 0);

            if (displacement == new Vector3(0, 0, 0))
                return;

            displacement.Normalize();

            const float scaleFactor = 4;
            Matrix rotationMatrix = Matrix.CreateFromQuaternion(mCameraQuaternion);
            Vector3 transformedDisplacement = Vector3.Transform(displacement * scaleFactor, rotationMatrix);
            mPosition += transformedDisplacement;
        }

        void UpdateViewRotation()
        {
            MouseState MS = Mouse.GetState();
            Vector2 mousePos = new Vector2(MS.X, MS.Y);
            Vector2 input = Vector2.Subtract(mousePos, new Vector2(mHalfWindowSize[0], mHalfWindowSize[1]));

            const float xRotationSpeed = 0.01f;
            const float yRotationSpeed = 0.01f;
            mRotation.X += input.X * xRotationSpeed;
            mRotation.Y += input.Y * yRotationSpeed;

            if (mRotation.Y > 1.4f) mRotation.Y = 1.4f;
            if (mRotation.Y < -1.4f) mRotation.Y = -1.4f;

            if (mRotation.X > (float)Math.PI) mRotation.X -= 2 * (float)Math.PI;
            if (mRotation.X < -(float)Math.PI) mRotation.X += 2 * (float)Math.PI;
        }

        void GenerateViewMatrix()
        {
            Quaternion xRotationQuaternion = Quaternion.CreateFromAxisAngle(new Vector3(0, -1, 0), mRotation.X);
            Quaternion yRotationQuaternion = Quaternion.CreateFromAxisAngle(new Vector3(-1, 0, 0), mRotation.Y);
            mCameraQuaternion = xRotationQuaternion * yRotationQuaternion;
            mView = Matrix.Invert(Matrix.CreateFromQuaternion(mCameraQuaternion) * Matrix.CreateTranslation(mPosition));
        }

        public Matrix View
        {
            get { return mView; }
        }

        public Vector3 Position
        {
            get { return mPosition; }
        }

        public void Update()
        {
            UpdateViewRotation();
            UpdatePosition();
            GenerateViewMatrix();

            ResetMouse();
        }
    }
}
