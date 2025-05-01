using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VCSpacePhysics.Ship.Controls
{
    class YawPitchUI : MonoBehaviour
    {
        public enum YawPitchUIType
        {
            Spatial,
            Camera
        }

        public static List<YawPitchUI> _instances = new List<YawPitchUI>();

        public YawPitchUIType type = YawPitchUIType.Spatial;

        public HelmExtras helmExtras;

        private Canvas canvas;

        private GameObject outerCircle;
        private GameObject innerCircle;

        public bool IsVisible;

        const float outerCircleDiameter = 0.5f;

        public void Awake()
        {
            _instances.Add(this);

            var rectTransform = gameObject.AddComponent<RectTransform>();
            rectTransform.anchoredPosition3D = new Vector3(-0.0121f, 1.8051f, -2.16f);
            rectTransform.sizeDelta = new Vector2(0.2f, 0.2f);

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            outerCircle = new GameObject();
            outerCircle.transform.SetParent(gameObject.transform);

            var outerCircleRectTransform = outerCircle.AddComponent<RectTransform>();
            outerCircleRectTransform.anchoredPosition3D = Vector3.zero;
            outerCircleRectTransform.sizeDelta = new Vector2(outerCircleDiameter, outerCircleDiameter);

            var outerCircleCircle = outerCircle.AddComponent<Circle>();
            outerCircleCircle.color = Color.white;
            outerCircleCircle.lineWeight = 0.003f;
            outerCircleCircle.filled = false;
            outerCircleCircle.segments = 128;



            innerCircle = new GameObject();
            innerCircle.transform.SetParent(gameObject.transform);

            var innerCircleRectTransform = innerCircle.AddComponent<RectTransform>();
            innerCircleRectTransform.anchoredPosition3D = Vector2.zero;
            innerCircleRectTransform.sizeDelta = new Vector2(0.02f, 0.02f);

            var innerCircleCircle = innerCircle.AddComponent<Circle>();
            innerCircleCircle.color = Color.white;
            innerCircleCircle.filled = true;
            innerCircleCircle.segments = 12;
        }

        public void Update()
        {
            if (helmExtras == null)
            {
                return;
            }

            switch (type)
            {
                case YawPitchUIType.Spatial:
                    IsVisible = helmExtras._helm.IsPowered;
                    gameObject.transform.localPosition = new Vector3(0, 1.75f, -2.2f); // Not necessary to do every frame at all, I just wanted this code in the same place as the bit below.
                    break;
                case YawPitchUIType.Camera:
                default:
                    IsVisible = helmExtras._helm.IsPowered && helmExtras.controllingYawPitch;
                    gameObject.transform.localPosition = new Vector3(0, 0, 1.075f); // For some reason this doesn't keep its initial position. Putting it back every frame is overkill but it's an easy fix.
                    break;
            }

            outerCircle.SetActive(IsVisible);
            innerCircle.SetActive(IsVisible);

            if (!IsVisible)
            {
                return;
            }

            ((RectTransform)innerCircle.transform).anchoredPosition = helmExtras.yawPitchInput * outerCircleDiameter / 2f;
        }

        public void OnDestroy()
        {
            _instances.Remove(this);
        }

        public Vector2? GetLookingPosition(Ray ray)
        {
            var canvasPlane = new Plane(canvas.transform.forward, canvas.transform.position);

            float hitDistance;
            if (canvasPlane.Raycast(ray, out hitDistance))
            {
                var hitLocationWorldspace = ray.GetPoint(hitDistance);
                var hitLocationCanvasspace = canvas.transform.InverseTransformPoint(hitLocationWorldspace);
                return hitLocationCanvasspace / (outerCircleDiameter / 2f);
            }
            return null;
        }
    }
}
