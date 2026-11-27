using System.Collections.Generic;
using IronKnuckle.Data;
using UnityEngine;

namespace IronKnuckle.Robot
{
    public class RobotBuilder : MonoBehaviour
    {
        public Transform PelvisPivot { get; private set; }
        public Transform TorsoPivot { get; private set; }
        public Transform HeadPivot { get; private set; }
        public Transform LeftShoulderPivot { get; private set; }
        public Transform LeftElbowPivot { get; private set; }
        public Transform RightShoulderPivot { get; private set; }
        public Transform RightElbowPivot { get; private set; }
        public Transform LeftLegPivot { get; private set; }
        public Transform RightLegPivot { get; private set; }
        public Transform CorePivot { get; private set; }
        public Transform LeftGloveTip { get; private set; }
        public Transform RightGloveTip { get; private set; }

        public VisorFaceController VisorController { get; private set; }
        public TrailRenderer LeftGloveTrail { get; private set; }
        public TrailRenderer RightGloveTrail { get; private set; }

        private readonly List<Renderer> partRenderers = new List<Renderer>();

        private MeshFilter headFilter;
        private MeshFilter torsoFilter;
        private MeshFilter leftGloveFilter;
        private MeshFilter rightGloveFilter;
        private MeshFilter leftLegFilter;
        private MeshFilter rightLegFilter;
        private MeshFilter coreFilter;
        private Renderer visorRenderer;

        private Material primaryMat;
        private Material gunmetalMat;
        private Material emissiveMat;
        private Material visorMat;

        private ParticleSystem swapSparkPs;

        private bool isBuilt;
        private static readonly int HitFlashId = Shader.PropertyToID("_HitFlash");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int SpecSmoothnessId = Shader.PropertyToID("_SpecSmoothness");
        private static readonly int MetallicSpecId = Shader.PropertyToID("_MetallicSpec");
        private MaterialPropertyBlock flashMpb;

        public void EnsureSkeleton(GameDatabase db)
        {
            if (isBuilt) return;
            isBuilt = true;

            Shader fallback = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            primaryMat = db != null && db.robotBaseMaterial != null ? new Material(db.robotBaseMaterial) : new Material(fallback);
            gunmetalMat = db != null && db.gunmetalBaseMaterial != null ? new Material(db.gunmetalBaseMaterial) : new Material(primaryMat);
            emissiveMat = db != null && db.emissiveBaseMaterial != null ? new Material(db.emissiveBaseMaterial) : new Material(primaryMat);
            visorMat = db != null && db.visorBaseMaterial != null
                ? new Material(db.visorBaseMaterial)
                : new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? fallback);

            PelvisPivot = CreateNode("Pelvis", transform, new Vector3(0f, 0.88f, 0f));
            AddMeshPart("PelvisMesh", PelvisPivot, db?.pelvisMesh, Vector3.zero, Vector3.one);

            LeftLegPivot = CreateNode("LeftLeg", PelvisPivot, new Vector3(-0.22f, -0.04f, 0f));
            leftLegFilter = AddMeshPart("LeftLegMesh", LeftLegPivot, null, Vector3.zero, Vector3.one);

            RightLegPivot = CreateNode("RightLeg", PelvisPivot, new Vector3(0.22f, -0.04f, 0f));
            rightLegFilter = AddMeshPart("RightLegMesh", RightLegPivot, null, Vector3.zero, new Vector3(-1f, 1f, 1f));

            TorsoPivot = CreateNode("Torso", PelvisPivot, new Vector3(0f, 0.12f, 0f));
            torsoFilter = AddMeshPart("TorsoMesh", TorsoPivot, null, Vector3.zero, Vector3.one);

            CorePivot = CreateNode("Core", TorsoPivot, new Vector3(0f, 0.45f, -0.22f));
            coreFilter = AddMeshPart("CoreMesh", CorePivot, null, Vector3.zero, Vector3.one);

            HeadPivot = CreateNode("Head", TorsoPivot, new Vector3(0f, 0.86f, 0f));
            headFilter = AddMeshPart("HeadMesh", HeadPivot, null, Vector3.zero, Vector3.one);

            var visorFilter = AddVisorPart("VisorScreen", HeadPivot, db?.visorScreenMesh, new Vector3(0f, 0.23f, 0.215f));
            visorRenderer = visorFilter.GetComponent<Renderer>();
            VisorController = gameObject.GetComponent<VisorFaceController>();
            if (VisorController == null) VisorController = gameObject.AddComponent<VisorFaceController>();

            LeftShoulderPivot = CreateNode("LeftShoulder", TorsoPivot, new Vector3(-0.49f, 0.66f, 0f));
            AddMeshPart("LShoulderPad", LeftShoulderPivot, db?.shoulderPadMesh, Vector3.zero, Vector3.one);
            AddMeshPart("LUpperArm", LeftShoulderPivot, db?.upperArmMesh, Vector3.zero, Vector3.one);
            LeftElbowPivot = CreateNode("LeftElbow", LeftShoulderPivot, new Vector3(0f, -0.44f, 0f));
            leftGloveFilter = AddMeshPart("LeftGloveMesh", LeftElbowPivot, null, Vector3.zero, Vector3.one);
            LeftGloveTip = CreateNode("LeftGloveTip", LeftElbowPivot, new Vector3(0f, 0f, 0.46f));
            LeftGloveTrail = CreateGloveTrail(LeftGloveTip);

            RightShoulderPivot = CreateNode("RightShoulder", TorsoPivot, new Vector3(0.49f, 0.66f, 0f));
            AddMeshPart("RShoulderPad", RightShoulderPivot, db?.shoulderPadMesh, Vector3.zero, Vector3.one);
            AddMeshPart("RUpperArm", RightShoulderPivot, db?.upperArmMesh, Vector3.zero, Vector3.one);
            RightElbowPivot = CreateNode("RightElbow", RightShoulderPivot, new Vector3(0f, -0.44f, 0f));
            rightGloveFilter = AddMeshPart("RightGloveMesh", RightElbowPivot, null, Vector3.zero, new Vector3(-1f, 1f, 1f));
            RightGloveTip = CreateNode("RightGloveTip", RightElbowPivot, new Vector3(0f, 0f, 0.46f));
            RightGloveTrail = CreateGloveTrail(RightGloveTip);
        }

        public void ApplyLoadout(
            GameDatabase db,
            string headId,
            string torsoId,
            string leftArmId,
            string rightArmId,
            string legsId,
            string coreId,
            string colorSchemeId,
            int finishIndex = 0)
        {
            if (db == null) return;
            EnsureSkeleton(db);

            ApplyPart(db.GetPart(headId), headFilter, Vector3.one);
            ApplyPart(db.GetPart(torsoId), torsoFilter, Vector3.one);
            ApplyPart(db.GetPart(leftArmId), leftGloveFilter, Vector3.one);
            ApplyPart(db.GetPart(rightArmId), rightGloveFilter, new Vector3(-1f, 1f, 1f));
            ApplyPart(db.GetPart(legsId), leftLegFilter, Vector3.one);
            ApplyPart(db.GetPart(legsId), rightLegFilter, new Vector3(-1f, 1f, 1f));
            ApplyPart(db.GetPart(coreId), coreFilter, Vector3.one);

            var scheme = db.GetColorScheme(colorSchemeId);
            ApplyColorScheme(scheme);
            ApplySurfaceFinish(db, finishIndex, scheme);
        }

        public void ApplyColorScheme(ColorSchemeData scheme)
        {
            if (scheme == null) return;
            if (primaryMat != null)
            {
                primaryMat.SetColor(BaseColorId, scheme.primaryColor);
                primaryMat.SetColor(RimColorId, scheme.visorColor);
            }
            if (gunmetalMat != null)
            {
                gunmetalMat.SetColor(BaseColorId, scheme.secondaryColor);
                gunmetalMat.SetColor(RimColorId, scheme.visorColor * 0.55f);
            }
            if (emissiveMat != null)
            {
                emissiveMat.SetColor(BaseColorId, scheme.accentColor);
                emissiveMat.SetColor(RimColorId, Color.white);
                emissiveMat.SetColor(EmissionColorId, scheme.accentColor * 1.45f);
            }

            if (VisorController != null)
            {
                VisorController.Initialize(visorRenderer, scheme.visorColor);
            }

            UpdateTrailColor(LeftGloveTrail, scheme.accentColor);
            UpdateTrailColor(RightGloveTrail, scheme.accentColor);
        }

        public void ApplySurfaceFinish(GameDatabase db, int finishIndex, ColorSchemeData scheme)
        {
            if (db == null || primaryMat == null) return;
            var tex = db.GetFinishTexture(finishIndex);
            if (tex != null) primaryMat.SetTexture(BaseMapId, tex);

            switch ((SurfaceFinish)Mathf.Clamp(finishIndex, 0, 4))
            {
                case SurfaceFinish.BrushedChrome:
                    primaryMat.SetFloat(MetallicSpecId, 1.85f);
                    primaryMat.SetFloat(SpecSmoothnessId, 0.78f);
                    primaryMat.SetColor(EmissionColorId, Color.black);
                    break;
                case SurfaceFinish.CarbonFiber:
                    primaryMat.SetFloat(MetallicSpecId, 1.15f);
                    primaryMat.SetFloat(SpecSmoothnessId, 0.88f);
                    primaryMat.SetColor(EmissionColorId, Color.black);
                    break;
                case SurfaceFinish.HazardIndustrial:
                    primaryMat.SetFloat(MetallicSpecId, 0.65f);
                    primaryMat.SetFloat(SpecSmoothnessId, 0.86f);
                    primaryMat.SetColor(EmissionColorId, Color.black);
                    break;
                case SurfaceFinish.NeonCircuit:
                    primaryMat.SetFloat(MetallicSpecId, 1.1f);
                    primaryMat.SetFloat(SpecSmoothnessId, 0.84f);
                    Color glow = scheme != null ? scheme.visorColor : new Color(0.2f, 0.9f, 1f);
                    primaryMat.SetColor(EmissionColorId, glow * 0.25f);
                    break;
                default:
                    primaryMat.SetFloat(MetallicSpecId, 0.80f);
                    primaryMat.SetFloat(SpecSmoothnessId, 0.86f);
                    primaryMat.SetColor(EmissionColorId, Color.black);
                    break;
            }
        }

        public void PlaySwapSparks(PartSlot? slot = null)
        {
            if (!Application.isPlaying) return;
            if (swapSparkPs == null)
            {
                var go = new GameObject("SwapSparks");
                go.transform.SetParent(transform, false);
                swapSparkPs = go.AddComponent<ParticleSystem>();
                var main = swapSparkPs.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.35f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 5.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
                main.startColor = new Color(0.35f, 0.95f, 1f);
                main.gravityModifier = 0.8f;
                var em = swapSparkPs.emission;
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
                var sh = swapSparkPs.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.22f;
                var rend = go.GetComponent<ParticleSystemRenderer>();
                rend.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
            }

            Transform target = TorsoPivot;
            if (slot.HasValue)
            {
                switch (slot.Value)
                {
                    case PartSlot.Head: target = HeadPivot; break;
                    case PartSlot.Torso: target = TorsoPivot; break;
                    case PartSlot.LeftArm: target = LeftElbowPivot; break;
                    case PartSlot.RightArm: target = RightElbowPivot; break;
                    case PartSlot.Legs: target = PelvisPivot; break;
                    case PartSlot.Core: target = CorePivot; break;
                }
            }
            if (target != null) swapSparkPs.transform.position = target.position;
            swapSparkPs.Play();
        }

        public void SetHitFlash(float amount)
        {
            if (flashMpb == null) flashMpb = new MaterialPropertyBlock();
            for (int i = 0; i < partRenderers.Count; i++)
            {
                var r = partRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(flashMpb);
                flashMpb.SetFloat(HitFlashId, amount);
                r.SetPropertyBlock(flashMpb);
            }
        }

        public void SetTrailsEmitting(bool left, bool right)
        {
            if (LeftGloveTrail != null) LeftGloveTrail.emitting = left;
            if (RightGloveTrail != null) RightGloveTrail.emitting = right;
        }

        private void ApplyPart(RobotPartData part, MeshFilter filter, Vector3 mirror)
        {
            if (filter == null || part == null) return;
            if (part.mesh != null)
            {
                filter.sharedMesh = part.mesh;
                var mr = filter.GetComponent<MeshRenderer>();
                BindSubmeshMaterials(mr, part.mesh);
            }
            Vector3 s = part.meshScale == Vector3.zero ? Vector3.one : part.meshScale;
            filter.transform.localScale = Vector3.Scale(s, mirror);
        }

        private void BindSubmeshMaterials(MeshRenderer mr, Mesh mesh)
        {
            if (mr == null) return;
            if (mesh != null && mesh.subMeshCount >= 3)
            {
                mr.sharedMaterials = new[] { primaryMat, gunmetalMat, emissiveMat };
            }
            else if (mesh != null && mesh.subMeshCount == 2)
            {
                mr.sharedMaterials = new[] { primaryMat, gunmetalMat };
            }
            else
            {
                mr.sharedMaterials = new[] { primaryMat };
            }
        }

        private Transform CreateNode(string nodeName, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(nodeName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        private MeshFilter AddMeshPart(string partName, Transform parent, Mesh mesh, Vector3 localPos, Vector3 localScale)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            var mf = go.AddComponent<MeshFilter>();
            if (mesh != null) mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            BindSubmeshMaterials(mr, mesh);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            partRenderers.Add(mr);
            return mf;
        }

        private MeshFilter AddVisorPart(string partName, Transform parent, Mesh mesh, Vector3 localPos)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            var mf = go.AddComponent<MeshFilter>();
            if (mesh != null) mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = visorMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mf;
        }

        private TrailRenderer CreateGloveTrail(Transform parent)
        {
            var go = new GameObject("GloveTrail");
            go.transform.SetParent(parent, false);
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.16f;
            tr.startWidth = 0.22f;
            tr.endWidth = 0.02f;
            tr.emitting = false;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            tr.sharedMaterial = new Material(shader);
            return tr;
        }

        private void UpdateTrailColor(TrailRenderer tr, Color c)
        {
            if (tr == null) return;
            tr.startColor = new Color(c.r, c.g, c.b, 0.85f);
            tr.endColor = new Color(c.r, c.g, c.b, 0f);
            if (tr.sharedMaterial != null) tr.sharedMaterial.color = c;
        }
    }
}
