---
license: apache-2.0
base_model: speridlabs/idol-turbo
base_model_relation: finetune
pipeline_tag: image-to-3d
library_name: idol
tags:
- budgetany
- budget3d
- adaptive-tokenization
- gaussian-splatting
- 3d
- image-to-3d
- turbo
- few-step
---

# Budget3D-Idol-Turbo

🌐 [Project page](https://budgetany.github.io/budget3d/) &nbsp;|&nbsp; 💻 [Code](https://github.com/BudgetAny/Budget3D) &nbsp;|&nbsp; 📄 Tech report (coming soon) · [BudgetPix paper](https://arxiv.org/abs/2610.12307) &nbsp;|&nbsp; 🧩 [BudgetAny](https://huggingface.co/BudgetAny)

**One checkpoint, any token budget, 5 sampling steps.** Budget3D-Idol-Turbo is a fine-tune of
[speridlabs/idol-turbo](https://huggingface.co/speridlabs/idol-turbo), the 5-step guidance-free distillation of the
single-image to 3D Gaussian splat generator [Idol](https://huggingface.co/speridlabs/idol). At inference you choose
how many of Idol's 8,192 latent tokens the transformer runs on: the regions where the latent varies least, empty space
first, are merged into coarser tokens, and sampling gets faster and lighter. The same weights serve every budget;
Idol's image encoders and Gaussian decoders are used unchanged. The 50-step model is
[BudgetAny/Budget3D-Idol](https://huggingface.co/BudgetAny/Budget3D-Idol).

> **Unofficial, open-source implementation.** This model is part of BudgetAny, a community reimplementation and
> extension of the compute-adaptive tokenization idea of
> [*BudgetPix: Compute-Adaptive Tokenization for Pixel-Space Image Diffusion*](https://arxiv.org/abs/2610.12307) (Kara et al., 2026). It is not
> an official release of that paper, and BudgetAny is not a company or a lab. The paper covers pixel-space image
> diffusion only; applying the idea to image-to-3D is an extension made here, and the numbers on this card are
> measurements of this project, not results from the paper. Thanks to the BudgetPix authors for the method.

## Files

| file | base model | dense tokens | parameters | size | SHA-256 |
|---|---|---|---|---|---|
| `budget3d_idol_turbo.safetensors` | Idol-turbo (5 sampling steps) | 8,192 | 407 M | 1.63 GB | `4f0c31ed6979f804…` |

The file holds the fine-tuned denoiser (Idol's DiT, Idol-turbo's two LoRA experts and the Budget3D modules) as fp32
EMA weights. Also in the repository: `perf/` (the complete speed and memory reports) and
`assets/` (the clips below, and the same objects from the stock Idol-turbo at 100 % in
`assets/stock_idol_turbo/`). Idol's image encoders and Gaussian decoders are downloaded
from [speridlabs/idol](https://huggingface.co/speridlabs/idol) by the code.

## Usage

```bash
git clone https://github.com/BudgetAny/Budget3D && cd Budget3D    # setup: see README.md
hf download BudgetAny/Budget3D-Idol-Turbo budget3d_idol_turbo.safetensors config.json --local-dir weights
python scripts/sample.py --ckpt weights/budget3d_idol_turbo.safetensors --turbo --image photo.jpg --budget 0.5 --out photo.ply
```

`--budget` is the fraction of the 8,192 tokens (0.0625 to 1); `--fast` switches on the optimised inference.

## Speed and memory

One NVIDIA RTX PRO 6000 Blackwell Server Edition, 5 Euler steps without guidance, batches of 8 objects, optimised inference (`--fast`): bf16 transformer blocks, TF32 elsewhere, cuDNN fused attention, torch.compile. Times are per object, averaged over the 160 objects in full batches after each GPU's warm-up batch; the speed-up is the sampling time at 100 % divided by the sampling time at the budget, for the same batch.

| budget | tokens | sampling / object | speed-up | peak sampling memory | end to end / object |
|---:|---:|---:|---:|---:|---:|
| 100 % | 8,192 | 0.48 s | 1.00× | 8.0 GB | 3.64 s |
| 90 % | 7,373 | 0.44 s | 1.09× | 7.8 GB | 3.51 s |
| 80 % | 6,554 | 0.39 s | 1.22× | 7.5 GB | 3.45 s |
| 70 % | 5,734 | 0.35 s | 1.36× | 7.3 GB | 3.37 s |
| 60 % | 4,915 | 0.31 s | 1.52× | 7.1 GB | 3.25 s |
| 50 % | 4,096 | 0.27 s | 1.74× | 6.9 GB | 3.27 s |
| 40 % | 3,277 | 0.25 s | 1.95× | 6.6 GB | 3.39 s |
| 30 % | 2,458 | 0.22 s | 2.21× | 6.4 GB | 3.21 s |
| 20 % | 1,638 | 0.17 s | 2.74× | 6.2 GB | 3.15 s |
| 10 % | 819 | 0.15 s | 3.22× | 6.0 GB | 3.23 s |

Peak memory includes about 4.6 GB of resident weights and encoders.
End to end adds decoding the Gaussians (0.92 s) and rendering a 72-frame orbit (2.25 s), which do not depend on the budget. The speed-up is smaller than the token saving because every sequence also carries 4,102
image-condition tokens that the budget does not shorten. At 100 % the model costs the same as the stock Idol-turbo in
the same setting (0.47 s per object, median). The complete
reports are in `perf/`.

## Examples

Each clip shows the input image and the same object generated at 100 % down to 10 % of the tokens, from the same noise
and camera orbit. The objects are not picked by quality: they are every 51st of the 200 test objects
(2 example images from the Idol repository and 2 held-out Dome-Objaverse objects).

<video src="https://huggingface.co/BudgetAny/Budget3D-Idol-Turbo/resolve/main/assets/fig-knight.mp4" width="100%" autoplay loop muted playsinline controls></video>

<video src="https://huggingface.co/BudgetAny/Budget3D-Idol-Turbo/resolve/main/assets/fur-lounge-velvet-green.mp4" width="100%" autoplay loop muted playsinline controls></video>

<video src="https://huggingface.co/BudgetAny/Budget3D-Idol-Turbo/resolve/main/assets/dome-101-519185.mp4" width="100%" autoplay loop muted playsinline controls></video>

<video src="https://huggingface.co/BudgetAny/Budget3D-Idol-Turbo/resolve/main/assets/dome-57-299669.mp4" width="100%" autoplay loop muted playsinline controls></video>

## Citation

Tech report coming soon. Until then, please cite the BudgetPix paper:

```bibtex
@article{kara2026budgetpix,
  title={BudgetPix: Compute-Adaptive Tokenization for Pixel-Space Image Diffusion},
  author={Ozgur Kara and Yujia Chen and Daniel Watson and David Forsyth and James Matthew Rehg and Wen-Sheng Chu and Du Tran},
  journal={arXiv preprint arXiv:2610.12307},
  year={2026}
}
```

Built on [Idol-turbo](https://huggingface.co/speridlabs/idol-turbo) and [Idol](https://huggingface.co/speridlabs/idol) by Sperid Labs, [DINOv3](https://github.com/facebookresearch/dinov3), the [FLUX.2](https://huggingface.co/black-forest-labs) VAE and [Dome-Objaverse](https://huggingface.co/datasets/zeyuanyin/Dome-Objaverse) renders of [Objaverse](https://objaverse.allenai.org/).

We acknowledge the compute support of the Health Care Engineering Systems Center within the Coordinated Science Lab at UIUC.

## License

Apache-2.0. The weights are a fine-tune of [speridlabs/idol-turbo](https://huggingface.co/speridlabs/idol-turbo),
which is released under Apache-2.0 and is itself derived from [speridlabs/idol](https://huggingface.co/speridlabs/idol)
(Apache-2.0). Running the model also needs DINOv3 ViT-H+/16 and the FLUX.2 VAE, which are not part of this
repository and have their own licenses.
