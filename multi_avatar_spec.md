# ContactLens 他アバター対応 改修仕様書

作成日: 2026-02-03

---

## 1. 概要

ContactLensを複数アバター（flat, comodo, fel, heon, kewf）に対応させるための改修仕様。
レンズテクスチャはflat基準で作成し、他アバターへは自動変換で適用する。

---

## 2. 対応アバター一覧

| 正規化名 | 説明 | 解像度 | 瞳孔方式 |
|---------|------|--------|---------|
| flat12 | flat Ver1&2 | 2048 | テクスチャ一体 |
| flat3if | flat Ver3&If | 2048 | 独立アイランド |
| comodo | comodo | 2048 | テクスチャ一体 |
| fel | fel | 4096 | テクスチャ一体 |
| heon | heon | 4096 | 独立アイランド |
| kewf | kewf | 4096 | 独立アイランド |

### 瞳孔方式の違い

- **テクスチャ一体**: 瞳孔が顔テクスチャに描かれている。レンズ上書きで対応。
- **独立アイランド**: 同一メッシュ内で瞳孔が別のUVアイランド。頂点ずらしで非表示化可能。

---

## 3. マスク画像仕様

### 3.1 マスク種別

| 名称 | 用途 | 対象 |
|------|------|------|
| eyeArea | 目エリア（レンズ貼り範囲）| 全アバター |
| pupilTexture | 瞳孔上書き範囲 | 全アバター |
| pupilIsland | 瞳孔アイランド位置（頂点ずらし判定 + α=1時の色塗り範囲）| 独立アイランド式のみ |

### 3.2 ファイル配置

```
Assets/Pan/ContactLens/texture/Mask/
├── flat12/
│   ├── eyeArea.png
│   └── pupilTexture.png
├── flat3if/
│   ├── eyeArea.png
│   ├── pupilTexture.png
│   └── pupilIsland.png
├── comodo/
│   ├── eyeArea.png
│   └── pupilTexture.png
├── fel/
│   ├── eyeArea.png
│   └── pupilTexture.png
├── heon/
│   ├── eyeArea.png
│   ├── pupilTexture.png
│   └── pupilIsland.png
└── kewf/
    ├── eyeArea.png
    ├── pupilTexture.png
    └── pupilIsland.png
```

### 3.3 マスク画像フォーマット

- PNG形式
- グレースケール（白=対象領域、黒=対象外）
- 解像度: 各アバターのテクスチャ解像度に合わせる（2048x2048 または 4096x4096）

### 3.4 必要マスク数

| アバター | eyeArea | pupilTexture | pupilIsland | 計 |
|---------|---------|--------------|-------------|-----|
| flat12 | ○ | ○ | - | 2 |
| flat3if | ○ | ○ | ○ | 3 |
| comodo | ○ | ○ | - | 2 |
| fel | ○ | ○ | - | 2 |
| heon | ○ | ○ | ○ | 3 |
| kewf | ○ | ○ | ○ | 3 |
| **合計** | 6 | 6 | 3 | **15** |

---

## 4. テクスチャ変換仕様

### 4.1 基準フォーマット

- レンズテクスチャは **flat基準** で作成
- flat12とflat3ifは同一UV配置（瞳孔方式のみ異なる）

### 4.2 変換パラメータ

eyeAreaマスクから以下を自動算出:

- 左目中心座標 (cx, cy) - UV座標 0.0〜1.0
- 右目中心座標 (cx, cy) - UV座標 0.0〜1.0
- 目のサイズ (width, height) - UV座標単位
- アスペクト比 (height / width)

### 4.3 変換処理

flatのレンズテクスチャを対象アバターに変換する際:

1. flat基準の目領域を抽出
2. 対象アバターの目の位置・サイズ・アスペクト比に合わせて変形
3. 対象アバターのテクスチャ解像度にリサイズ

---

## 5. JSON設定ファイル仕様

### 5.1 ファイル配置

```
Assets/Pan/ContactLens/config/avatars.json
```

### 5.2 JSON構造

```json
{
  "avatars": {
    "flat12": {
      "displayName": "flat Ver1&2",
      "resolution": 2048,
      "pupilType": "texture",
      "leftEye": {
        "cx": 0.15,
        "cy": 0.85,
        "width": 0.12,
        "height": 0.15
      },
      "rightEye": {
        "cx": 0.85,
        "cy": 0.85,
        "width": 0.12,
        "height": 0.15
      }
    },
    "flat3if": {
      "displayName": "flat Ver3&If",
      "resolution": 2048,
      "pupilType": "island",
      "leftEye": { ... },
      "rightEye": { ... }
    },
    "comodo": {
      "displayName": "comodo",
      "resolution": 2048,
      "pupilType": "texture",
      "leftEye": { ... },
      "rightEye": { ... }
    },
    "fel": {
      "displayName": "fel",
      "resolution": 4096,
      "pupilType": "texture",
      "leftEye": { ... },
      "rightEye": { ... }
    },
    "heon": {
      "displayName": "heon",
      "resolution": 4096,
      "pupilType": "island",
      "leftEye": { ... },
      "rightEye": { ... }
    },
    "kewf": {
      "displayName": "kewf",
      "resolution": 4096,
      "pupilType": "island",
      "leftEye": { ... },
      "rightEye": { ... }
    }
  },
  "baseAvatar": "flat12"
}
```

### 5.3 フィールド説明

| フィールド | 型 | 説明 |
|-----------|-----|------|
| displayName | string | UI表示用の名前 |
| resolution | int | テクスチャ解像度 |
| pupilType | string | "texture"（テクスチャ一体）または "island"（独立アイランド）|
| leftEye.cx, cy | float | 左目中心のUV座標 (0.0〜1.0) |
| leftEye.width, height | float | 左目のUVサイズ |
| rightEye.* | float | 右目の同様のパラメータ |
| baseAvatar | string | レンズテクスチャの基準アバター |

---

## 6. コード改修項目

### 6.1 アイランド検出ロジック修正

**対象**: `ContactLens.cs` の `FindPupilVertices` メソッド

**現状**:
- 頂点数 (`PupilVertexCount = 100`) でフィルタ
- 中心Y座標 (`PupilYRange`) でフィルタ
- flatにハードコード

**改修後**:
- pupilIslandマスクを読み込み
- 各アイランドの頂点のUV座標を取得
- マスク上でそのUV位置が白ならば瞳孔アイランドと判定

**削除するハードコード**:
```csharp
const int PupilVertexCount = 100;
static readonly Vector2 PupilYRange = new Vector2(-0.19f, -0.175f);
```

### 6.2 テクスチャ変換ロジック作成

**対象**: `ContactLens.cs` に新規メソッド追加

**処理内容**:
1. JSONから基準アバター（flat）と対象アバターのパラメータ取得
2. レンズテクスチャの目領域を抽出
3. 位置・スケール・アスペクト比を変換
4. 対象アバターの解像度でテクスチャ生成

### 6.3 JSON読み込み機能

**対象**: 新規クラス `ContactLensConfig.cs`

**機能**:
- avatars.json の読み込み・パース
- アバター情報の取得API
- マスクパスの自動解決

### 6.4 ハードコード部分のJSON対応

**対象**: `ContactLens.cs`, `ContactLensEditor.cs`

**改修内容**:
- `AvatarMode` enum を廃止、JSONからアバター一覧を動的取得
- マスクパスをJSONベースで解決
- UIでアバター選択をドロップダウン化

---

## 7. パラメータ抽出ツール（Python）

### 7.1 概要

eyeAreaマスク画像からアバターパラメータを自動抽出し、avatars.jsonを生成/更新する。

### 7.2 処理内容

1. 各アバターのeyeArea.pngを読み込み
2. 白い領域を検出（左右の目を分離）
3. 各目の重心、バウンディングボックスを算出
4. UV座標（0.0〜1.0）に正規化
5. JSONに出力

### 7.3 実行方法

```bash
python extract_avatar_params.py
```

---

## 8. 作業手順

### Phase 1: マスク画像作成（手作業）

1. 各アバターのeyeAreaマスク作成（6枚）
2. 各アバターのpupilTextureマスク作成（6枚）
3. 独立アイランド式アバターのpupilIslandマスク作成（3枚）

### Phase 2: パラメータ抽出

1. PythonツールでeyeAreaからパラメータ抽出
2. avatars.json生成

### Phase 3: コード改修

1. ContactLensConfig.cs 作成（JSON読み込み）
2. FindPupilVertices 改修（マスクベース判定）
3. テクスチャ変換ロジック実装
4. AvatarMode enum 廃止、UI改修

### Phase 4: テスト

1. 各アバターでレンズ適用テスト
2. 瞳孔Hide動作確認（独立アイランド式）
3. テクスチャ変換品質確認

---

## 9. 互換性について

### 9.1 相互変換

理論上、任意のアバター間でレンズテクスチャを変換可能。

- flat → comodo
- comodo → flat
- heon → kewf
- など

### 9.2 基準アバター

- 製作者はflat基準でレンズを作成（推奨）
- 適用時に自動変換

---

## 10. 備考

- flatのUV配置はflat12とflat3ifで同一（瞳孔方式のみ異なる）
- マスク解像度は各アバターのテクスチャ解像度に合わせる
- 変換時の画質劣化を最小限にするため、高解像度での処理を検討
