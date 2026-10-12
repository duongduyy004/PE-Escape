using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using KMA.Gameplay;

namespace KMA.EditorTools
{
    public static class StudentJourneyContentBuilder
    {
        const string ChallengeFolder = "Assets/_Project/ScriptableObjects/Journey";
        const string ResourcesFolder = "Assets/_Project/Resources/Journey";
        const string CatalogPath = ResourcesFolder + "/ChallengeCatalog.asset";
        const string SprintBalancePath = ResourcesFolder + "/SprintBalance.asset";
        const string DialoguePath = ResourcesFolder + "/JourneyDialogues.asset";

        readonly struct ChallengeSpec
        {
            public readonly string Id;
            public readonly SubjectId Subject;
            public readonly ChallengeKind Kind;
            public readonly float Distance;
            public readonly float TimeLimit;
            public readonly int TargetCount;
            public readonly bool KeeperEnabled;
            public readonly ChallengeDifficulty Difficulty;
            public readonly bool TimingHelp;
            public readonly string Objective;
            public readonly int AttemptLimit;

            public ChallengeSpec(string id, SubjectId subject, ChallengeKind kind, float distance,
                float timeLimit, int targetCount, bool keeperEnabled, ChallengeDifficulty difficulty,
                bool timingHelp, string objective, int attemptLimit = 0)
            {
                Id = id;
                Subject = subject;
                Kind = kind;
                Distance = distance;
                TimeLimit = timeLimit;
                TargetCount = targetCount;
                KeeperEnabled = keeperEnabled;
                Difficulty = difficulty;
                TimingHelp = timingHelp;
                Objective = objective;
                AttemptLimit = attemptLimit;
            }
        }

        [MenuItem("KMA/Journey/Build Challenge Assets")]
        public static void BuildChallenges()
        {
            EnsureFolder(ChallengeFolder);
            EnsureFolder(ResourcesFolder);

            ChallengeSpec[] specs =
            {
                new ChallengeSpec("sprint_learn", SubjectId.Sprint, ChallengeKind.Learn,
                    0f, 0f, 50, false, ChallengeDifficulty.Normal, true,
                    "Bấm TRÁI, PHẢI luân phiên đúng 50 nhịp liên tiếp. Không giới hạn thời gian."),
                new ChallengeSpec("sprint_practice", SubjectId.Sprint, ChallengeKind.Practice,
                    150f, 20f, 0, false, ChallengeDifficulty.Normal, true,
                    "Chạy 150 m trong tối đa 20 giây."),
                new ChallengeSpec("sprint_exam", SubjectId.Sprint, ChallengeKind.Exam,
                    150f, 15f, 0, false, ChallengeDifficulty.Normal, false,
                    "Chạy 150 m trong tối đa 15 giây."),
                new ChallengeSpec("volleyball_learn", SubjectId.Volleyball, ChallengeKind.Learn,
                    0f, 0f, 3, false, ChallengeDifficulty.Easy, true,
                    "Đỡ bóng thành công 3 lần trong bài làm quen."),
                new ChallengeSpec("volleyball_practice", SubjectId.Volleyball, ChallengeKind.Practice,
                    0f, 120f, 5, false, ChallengeDifficulty.Easy, true,
                    "Ghi đủ 5 điểm trước đối thủ trong tối đa 120 giây."),
                new ChallengeSpec("volleyball_exam", SubjectId.Volleyball, ChallengeKind.Exam,
                    0f, 0f, 10, false, ChallengeDifficulty.Normal, false,
                    "Ghi đủ 10 điểm trước đối thủ."),
                new ChallengeSpec("soccer_learn", SubjectId.Football, ChallengeKind.Learn,
                    0f, 0f, 3, false, ChallengeDifficulty.Normal, true,
                    "Ghi 3 bàn vào khung thành không có thủ môn. Không giới hạn lượt sút."),
                new ChallengeSpec("soccer_practice", SubjectId.Football, ChallengeKind.Practice,
                    0f, 0f, 2, true, ChallengeDifficulty.Normal, true,
                    "Ghi ít nhất 2 bàn trong tối đa 6 lượt sút. Có thủ môn.", 6),
                new ChallengeSpec("soccer_exam", SubjectId.Football, ChallengeKind.Exam,
                    0f, 0f, 3, true, ChallengeDifficulty.Normal, false,
                    "Sút đủ 5 lượt và ghi ít nhất 3 bàn. Có thủ môn."),
                new ChallengeSpec("chess_final", SubjectId.Chess, ChallengeKind.Final,
                    0f, 90f, 2, false, ChallengeDifficulty.Normal, false,
                    "Chiếu hết trong 2 nước. Có 90 giây suy nghĩ và 2 lần sửa sai.")
            };

            var definitions = new ChallengeDefinition[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                ChallengeSpec spec = specs[i];
                string path = $"{ChallengeFolder}/{spec.Id}.asset";
                ChallengeDefinition definition = AssetDatabase.LoadAssetAtPath<ChallengeDefinition>(path);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<ChallengeDefinition>();
                    AssetDatabase.CreateAsset(definition, path);
                }

                Apply(definition, spec);
                definitions[i] = definition;
            }

            ChallengeCatalog catalog = AssetDatabase.LoadAssetAtPath<ChallengeCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ChallengeCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serializedCatalog = new SerializedObject(catalog);
            SerializedProperty challenges = serializedCatalog.FindProperty("challenges");
            challenges.arraySize = definitions.Length;
            for (int i = 0; i < definitions.Length; i++)
            {
                challenges.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            }
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

            if (!catalog.Validate(out string error))
            {
                throw new InvalidOperationException($"Generated challenge catalog is invalid: {error}");
            }

            EditorUtility.SetDirty(catalog);
            BuildSprintBalance();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KMA] Student journey challenge catalog built.");
        }

        [MenuItem("KMA/Journey/Build Sprint Balance")]
        public static void BuildSprintBalance()
        {
            EnsureFolder(ResourcesFolder);
            SprintBalanceConfig config = AssetDatabase.LoadAssetAtPath<SprintBalanceConfig>(SprintBalancePath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SprintBalanceConfig>();
                AssetDatabase.CreateAsset(config, SprintBalancePath);
            }

            SprintBalanceParameters value = SprintBalanceParameters.Default;
            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("correctImpulse").floatValue = value.CorrectImpulse;
            serialized.FindProperty("wrongImpulseFactor").floatValue = value.WrongImpulseFactor;
            serialized.FindProperty("speedCap").floatValue = value.SpeedCap;
            serialized.FindProperty("dragPerSecond").floatValue = value.DragPerSecond;
            serialized.FindProperty("distanceScale").floatValue = value.DistanceScale;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        const string CharacterArtFolder = "Assets/_Project/Art/Characters";
        const string TanThu = "tan_thu", Mai = "mai_toang", AnhKhoaTren = "anh_khoa_tren", Co = "co_the_chat";

        static readonly (DialoguePose Pose, string Suffix)[] PoseFiles =
        {
            (DialoguePose.Idle, "idle"), (DialoguePose.Cheer, "cheer0"), (DialoguePose.Hurt, "hurt"),
            (DialoguePose.Jump, "jump"), (DialoguePose.Duck, "duck")
        };

        // The hero and the teacher have their own reaction art; everyone else draws the shared poses.
        static readonly Dictionary<string, Dictionary<DialoguePose, string>> OwnPoseFiles =
            new Dictionary<string, Dictionary<DialoguePose, string>>
            {
                [CharacterArt.Hero] = new Dictionary<DialoguePose, string>
                    { [DialoguePose.Idle] = "fear", [DialoguePose.Cheer] = "happy", [DialoguePose.Hurt] = "disappointed" },
                [CharacterArt.Boss] = new Dictionary<DialoguePose, string>
                    { [DialoguePose.Idle] = "strictLook", [DialoguePose.Cheer] = "scoreWrite", [DialoguePose.Hurt] = "angry" }
            };

        const string EmojiSourceFolder = "Assets/_Project/Art/Emoji/Source~";
        const string EmojiFolder = "Assets/_Project/Art/Emoji";
        const string EmojiAtlasPath = EmojiFolder + "/JourneyEmojiAtlas.png";
        const string EmojiAssetPath = ResourcesFolder + "/JourneyEmoji.asset";
        const int EmojiCell = 72;
        const int EmojiColumns = 5;

        [MenuItem("KMA/Journey/Build Emoji Sprites")]
        public static void BuildEmojiSpriteAsset()
        {
            EnsureFolder(EmojiFolder);
            EnsureFolder(ResourcesFolder);
            IReadOnlyList<string> names = DialogueEmoji.KnownNames;
            int rows = (names.Count + EmojiColumns - 1) / EmojiColumns;
            var atlas = new Texture2D(EmojiColumns * EmojiCell, rows * EmojiCell, TextureFormat.RGBA32, false);
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
            var rects = new RectInt[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                string source = $"{EmojiSourceFolder}/{names[i]}.png";
                if (!File.Exists(source)) throw new InvalidOperationException("Missing emoji source: " + source);
                var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!image.LoadImage(File.ReadAllBytes(source)) || image.width != EmojiCell || image.height != EmojiCell)
                    throw new InvalidOperationException($"Emoji source must be a {EmojiCell}x{EmojiCell} PNG: {source}");
                int x = i % EmojiColumns * EmojiCell;
                int y = atlas.height - (i / EmojiColumns + 1) * EmojiCell;
                atlas.SetPixels32(x, y, EmojiCell, EmojiCell, image.GetPixels32());
                rects[i] = new RectInt(x, y, EmojiCell, EmojiCell);
                UnityEngine.Object.DestroyImmediate(image);
            }
            File.WriteAllBytes(EmojiAtlasPath, atlas.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(atlas);

            AssetDatabase.ImportAsset(EmojiAtlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(EmojiAtlasPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.isReadable = false;
            importer.SaveAndReimport();
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(EmojiAtlasPath);

            TMP_SpriteAsset asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(EmojiAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(asset, EmojiAssetPath);
            }
            asset.spriteSheet = sheet;
            if (asset.material == null)
            {
                var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "JourneyEmoji Material" };
                AssetDatabase.AddObjectToAsset(material, asset);
                asset.material = material;
            }
            asset.material.SetTexture(ShaderUtilities.ID_MainTex, sheet);

            // TMP treats an empty version as a legacy asset and rebuilds (empties) the tables on first
            // access, and throws while doing so. Stamp the version before touching any table.
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("m_Version").stringValue = "1.1.0";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            asset.spriteGlyphTable.Clear();
            asset.spriteCharacterTable.Clear();
            for (int i = 0; i < names.Count; i++)
            {
                var glyph = new TMP_SpriteGlyph((uint)i,
                    new GlyphMetrics(EmojiCell, EmojiCell, 0f, EmojiCell * .8f, EmojiCell),
                    new GlyphRect(rects[i].x, rects[i].y, EmojiCell, EmojiCell), 1f, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = names[i] });
            }
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[KMA] Journey emoji sprite asset built.");
        }

        [MenuItem("KMA/Journey/Build Dialogue Library")]
        public static void BuildDialogues()
        {
            // New dialogue art must be imported as sprites before it can be loaded below.
            CharacterArt.ImportAll();
            EnsureFolder(ResourcesFolder);
            BuildEmojiSpriteAsset();
            JourneyDialogueLibrary library = AssetDatabase.LoadAssetAtPath<JourneyDialogueLibrary>(DialoguePath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<JourneyDialogueLibrary>();
                AssetDatabase.CreateAsset(library, DialoguePath);
            }

            JourneyCharacter[] cast =
            {
                Character(TanThu, "SV năm 1", "#FFC928", true, "MaleAdventurer"),
                Character(Mai, "Mai", "#FF8FB1", false, "FemalePerson"),
                Character(AnhKhoaTren, "Anh Tuấn", "#7FD1FF", false, "MalePerson"),
                Character(Co, "Giảng viên thể chất", "#B9F27C", false, "BossPE")
            };
            JourneyDialogueNode[] nodes =
            {
                Node("opening",
                    Line(AnhKhoaTren, DialoguePose.Idle, "Buổi đầu nhớ nghe kỹ yêu cầu. Môn này không phải cứ có mặt là qua đâu."),
                    Line(Mai, DialoguePose.Idle, "Tớ cứ tưởng đầu kỳ chỉ điểm danh. Cậu có tập chạy trước không?"),
                    Line(TanThu, DialoguePose.Idle, "Chưa... Tớ chỉ mong không phải học lại ngay năm đầu."),
                    Line(Co, DialoguePose.Idle, "Chạy, bóng chuyền, bóng đá, rồi bài kiểm tra cuối. Đạt phần trước mới học phần sau. Không học được thì nợ.")),
                Node("sprint_intro",
                    Line(Co, DialoguePose.Idle, "Trái, phải luân phiên. Làm đúng 50 nhịp liên tiếp. Đừng để tôi phải nhắc lại chuyện trái với phải."),
                    Line(Mai, DialoguePose.Idle, "Bấm nhầm là đếm lại từ đầu. Cứ chậm một chút cho chắc."),
                    Line(TanThu, DialoguePose.Idle, "Ừ. Để tớ thử từng nhịp đã.")),
                Node("sprint_exam",
                    Line(Co, DialoguePose.Idle, "150 mét, tối đa 15 giây. Tôi bấm giờ, không nghe giải thích.", "15 GIÂY"),
                    Line(TanThu, DialoguePose.Idle, "Dạ. Mong là em giữ được nhịp như lúc tập.")),
                Node("sprint_pass",
                    Line(Co, DialoguePose.Cheer, "Tạm được. Đạt phần chạy. Đừng tưởng thế là xong học phần.", "ĐẠT"),
                    Line(Mai, DialoguePose.Idle, "Qua phần chạy rồi. Thở lại đã, còn bóng chuyền nữa.")),
                Node("volleyball_intro",
                    Line(Mai, DialoguePose.Idle, "Nhìn chỗ bóng rơi trước. Đứng lệch là đỡ hụt ngay."),
                    Line(Co, DialoguePose.Idle, "Bắt đầu bằng ba lần đỡ bóng. Mỗi bên chạm tối đa ba lần, không phải thích chạm bao nhiêu cũng được."),
                    Line(TanThu, DialoguePose.Idle, "Dạ. Cô cho em vào vị trí đã ạ.")),
                Node("volleyball_exam",
                    Line(Co, DialoguePose.Idle, "Bên nào đủ 10 điểm trước thì thắng. Bóng chạm sân rồi mới tiếc thì muộn.", "10 ĐIỂM"),
                    Line(Mai, DialoguePose.Idle, "Đừng nhìn bảng điểm suốt. Cứ theo bóng, xong lượt rồi tính."),
                    Line(TanThu, DialoguePose.Idle, "Ừ. Tớ chỉ sợ cuống rồi bấm sớm.")),
                Node("volleyball_pass",
                    Line(Co, DialoguePose.Cheer, "Đạt. Ít ra cậu đã biết đón bóng, không đứng nhìn nữa.", "ĐẠT"),
                    Line(TanThu, DialoguePose.Cheer, "Dạ... Qua được hai phần rồi.")),
                Node("soccer_intro",
                    Line(Mai, DialoguePose.Idle, "Đến bóng đá rồi. Tớ thấy lúc lấy lực dễ thả tay muộn lắm."),
                    Line(AnhKhoaTren, DialoguePose.Idle, "Anh cũng từng sút vọt xà vì giữ nút lâu quá. Đừng chỉ chăm chăm sút mạnh."),
                    Line(Co, DialoguePose.Idle, "Tập góc sút và lực sút trước. Bài thi có đúng năm lượt. Sút ra ngoài cũng tính một lượt.")),
                Node("soccer_exam",
                    Line(Co, DialoguePose.Idle, "Năm lượt sút, ít nhất ba bàn. Thủ môn không có nhiệm vụ giúp cậu qua môn.", "5 LƯỢT"),
                    Line(TanThu, DialoguePose.Idle, "Dạ. Em sẽ nhìn lại góc trước khi sút.")),
                Node("soccer_pass",
                    Line(Co, DialoguePose.Cheer, "Đạt phần bóng đá. Còn bài kiểm tra cuối với tôi. Chưa đến lúc cất đồ.", "ĐẠT"),
                    Line(Mai, DialoguePose.Idle, "Còn một bài nữa. Ngồi xuống cho đỡ run chân đã.")),
                Node("chess_intro",
                    Line(Co, DialoguePose.Idle, "Bài cuối là một thế cờ. Chiếu hết trong hai nước. Lần này dùng đầu, không dùng chân.", "BÀI CUỐI"),
                    Line(TanThu, DialoguePose.Idle, "Dạ, cờ vua ạ? Em chưa chơi nhiều..."),
                    Line(Co, DialoguePose.Idle, "Cậu có 90 giây suy nghĩ và hai lần sửa sai. Lần thứ ba sai là trượt. Đọc đề cho kỹ.", "90 GIÂY")),
                Node("supplementary",
                    Line(Co, DialoguePose.Idle, "Phần đã đạt tôi giữ nguyên. Phần chưa đạt thì ôn lại. Lần sau tôi vẫn chấm theo yêu cầu cũ."),
                    Line(Mai, DialoguePose.Idle, "Cậu hay vướng chỗ nào thì tập lại chỗ đó. Làm y như lần trước lại trượt thôi.")),
                Node("course_complete",
                    Line(Co, DialoguePose.Cheer, "Đạt học phần. Cậu không phải học lại với khóa sau. Nhớ giữ kết quả này.", "HOÀN THÀNH"),
                    Line(TanThu, DialoguePose.Cheer, "Dạ, em cảm ơn cô. Cuối cùng cũng qua rồi."),
                    Line(AnhKhoaTren, DialoguePose.Idle, "Qua rồi thì về nghỉ. Lần sau có môn thể chất, nhớ tập sớm hơn."),
                    Line(Mai, DialoguePose.Idle, "Đi thôi. Tớ đói từ lúc thi chạy đến giờ."))
            };

            library.SetContent(cast, nodes);
            if (!library.Validate(out string error))
                throw new InvalidOperationException("Generated journey dialogue library is invalid: " + error);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            // Unity writes a space after empty serialized strings; keep the generated YAML clean.
            File.WriteAllText(DialoguePath, string.Join("\n", File.ReadAllLines(DialoguePath)
                .Select(line => line.TrimEnd())) + "\n");
            Debug.Log("[KMA] Student journey dialogue library built.");
        }

        static JourneyCharacter Character(string id, string displayName, string hex, bool isPlayer, string spriteSet)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color color))
                throw new InvalidOperationException("Invalid tag color " + hex);
            var poses = new List<JourneyPoseSprite>();
            foreach ((DialoguePose pose, string suffix) in PoseFiles)
            {
                string file = OwnPoseFiles.TryGetValue(spriteSet, out var own) && own.TryGetValue(pose, out string mine)
                    ? mine : suffix;
                string path = $"{CharacterArtFolder}/{spriteSet}/{spriteSet}_{file}.png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) throw new InvalidOperationException("Missing dialogue sprite: " + path);
                poses.Add(new JourneyPoseSprite(pose, sprite));
            }
            return new JourneyCharacter(id, displayName, color, isPlayer, poses);
        }

        static JourneyDialogueNode Node(string id, params JourneyDialogueLine[] lines) =>
            new JourneyDialogueNode(id, new List<JourneyDialogueLine>(lines));

        static JourneyDialogueLine Line(string characterId, DialoguePose pose, string text, string sticker = "") =>
            new JourneyDialogueLine(characterId, pose, text, sticker);

        [MenuItem("KMA/Journey/Build All Journey Content")]
        public static void BuildAll()
        {
            BuildChallenges();
            BuildSprintBalance();
            BuildDialogues();
            GameplayDialogueContentBuilder.Build();
            ShellSceneAuthoring.Apply();
        }

        static void Apply(ChallengeDefinition definition, ChallengeSpec spec)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = spec.Id;
            serialized.FindProperty("subject").intValue = (int)spec.Subject;
            serialized.FindProperty("kind").intValue = (int)spec.Kind;
            serialized.FindProperty("distance").floatValue = spec.Distance;
            serialized.FindProperty("timeLimit").floatValue = spec.TimeLimit;
            serialized.FindProperty("targetCount").intValue = spec.TargetCount;
            serialized.FindProperty("attemptLimit").intValue = spec.AttemptLimit;
            serialized.FindProperty("keeperEnabled").boolValue = spec.KeeperEnabled;
            serialized.FindProperty("difficulty").intValue = (int)spec.Difficulty;
            serialized.FindProperty("timingHelp").boolValue = spec.TimingHelp;
            serialized.FindProperty("objective").stringValue = spec.Objective;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
