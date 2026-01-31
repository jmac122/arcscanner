# Tessdata Files

This folder should contain the Tesseract OCR trained data files.

## Required File

Download `eng.traineddata` from one of these sources:

1. **Best Quality (Recommended)**: [tessdata_best](https://github.com/tesseract-ocr/tessdata_best)
   - Direct link: https://github.com/tesseract-ocr/tessdata_best/raw/main/eng.traineddata

2. **Fast Mode**: [tessdata_fast](https://github.com/tesseract-ocr/tessdata_fast)
   - Direct link: https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata

## Installation

1. Download `eng.traineddata`
2. Place it in this folder (`Data/tessdata/`)
3. Restart the overlay application

## Notes

- File size for `tessdata_best` is approximately **15 MB**
- File size for `tessdata_fast` is approximately **4 MB**
- The `best` version provides higher accuracy but is slightly slower (recommended for game text)
- The `fast` version is optimized for speed with lower accuracy
