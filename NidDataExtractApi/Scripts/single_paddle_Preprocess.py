import warnings

import cv2
import numpy as np
import os
import pytesseract
import easyocr
import re
import json
import sys
from PIL import Image
from math import atan, degrees, radians, sin, cos, fabs
import warnings
from paddleocr import PaddleOCR
from datetime import datetime

det_model_dir_path = "D:/my_paddleocr_models/.paddleocr/whl/det/en/en_PP-OCRv3_det_infer"
rec_model_dir_path = "D:/my_paddleocr_models/.paddleocr/whl/rec/en/en_PP-OCRv4_rec_infer"
cls_model_dir_path = "D:/my_paddleocr_models/.paddleocr/whl/cls/ch_ppocr_mobile_v2.0_cls_infer"

sys.stdout.reconfigure(encoding='utf-8')

if len(sys.argv) < 2:
    print(json.dumps({"error": "Image path is required"}))
    sys.exit(1)

image_path = sys.argv[1]
if not os.path.exists(image_path):
    print(json.dumps({"error": f"Image file not found: {image_path}"}))
    sys.exit(1)

warnings.filterwarnings("ignore", category=UserWarning, module="paddle.utils.cpp_extension")

# Initialize PaddleOCR with these precise model directory paths
ocr = PaddleOCR(
    use_angle_cls=True,
    lang='en', # Keep lang='en' as your det and rec models are English
    use_gpu=False,
    show_log=False,
    det_model_dir=det_model_dir_path,
    rec_model_dir=rec_model_dir_path,
    cls_model_dir=cls_model_dir_path
)

# Code 1 Regex Patterns
fields_code1 = {
    'নাম': r'নাম[:：]*\s*([^\n:]+)',
    'Name': r'Name[:：]*\s*([^\n:]+)',
    'পিতা': r'পিতা[:：]*\s*([^\n:]+)',
    'মাতা': r'মাতা[:：]*\s*([^\n:]+)',
    'স্বামী': r'স্বামী[:：]*\s*([^\n:]+)',
    'স্ত্রী': r'স্ত্রী[:：]*\s*([^\n:]+)',
    'DateOfBirth': r'Date of Birth[:：]*\s*([^\n:]+)',
    'IDNO': r'(?:ID\s*NO|NID\s*No\.?|ID|NIDNo|NID\s*NO|NID\s*No|ID\s*N0)\s*[:：]*\s*([\d\s]{8,30})'
}

# Code 2 Regex Patterns
fields_code2 = {
    'নাম': r'নাম[:：]*\s*([^\n:]+)',
    'Name': r'Name[:：]*\s*([^\n:]+)',
    'পিতা': r'পিতা[:：]*\s*([^\n:]+)',
    'মাতা': r'মাতা[:：]*\s*([^\n:]+)',
    'স্বামী': r'(?:স্বামী|স্বা[:;মী-]*|husband|sami)[:;\s-]*(.+?)(?=\n|$|নাম|Name|পিতা|মাতা|স্ত্রী|Date|ID)',
    'স্ত্রী': r'(?:স্ত্রী|স্ত্র[:;ী-]*|wife|stri)[:;\s-]*(.+?)(?=\n|$|নাম|Name|পিতা|মাতা|স্বামী|Date|ID)',
    'DateOfBirth': r'(?:Date of Birth|DOB|Date|Birth)[:;\s-]*(\d{1,2}\s*(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s*\d{4}|\d{1,2}[-/]\d{1,2}[-/]\d{4})(?=\n|$|নাম|Name|পিতা|মাতা|স্বামী|স্ত্রী|ID)',
    'IDNO': r'(?:ID\s*NO|NID\s*No\.?|NIDNo|NID\s*NO|NID\s*No|ID\s*N0)\s*[:：]*\s*([\d\s]{8,30})'
}


# Code 1 Functions
def clean_header_text(text):
    keywords_to_remove = [
        "বাংলাদেশ সরকার", "জাতীয় পরিচয়", "জাতীয় পরিচয়", "National ID",
        "Government of the People's Republic","জাতীয় পরিচয্ন পত্র"
    ]
    cleaned_lines = []
    for line in text.splitlines():
        line_stripped = line.strip()
        if not any(keyword in line_stripped for keyword in keywords_to_remove):
            cleaned_lines.append(line_stripped)
    return "\n".join(cleaned_lines)


def infer_name_from_lines(text, extracted_fields):
    lines = [line.strip() for line in text.splitlines() if line.strip()]
    for i, line in enumerate(lines):
        if "Name" in line:
            if extracted_fields.get('নাম') in [None, "", "Not found"] and i > 0:
                extracted_fields['নাম'] = lines[i - 1]
            if extracted_fields.get('Name') in [None, "", "Not found"] and i + 1 < len(lines):
                extracted_fields['Name'] = lines[i + 1]
    return extracted_fields


def extract_fields_code1(text):
    extracted = {}
    for key, pattern in fields_code1.items():
        match = re.search(pattern, text, re.IGNORECASE | re.MULTILINE)
        if match:
            value = match.group(1).strip()
            # Condition 2: Skip if value is empty
            if not value:
                extracted[key] = "Not found"
                continue
            # Clean IDNO by removing spaces
            if key == 'IDNO':
                value = value.replace(" ", "")
            # Condition 1: Check for single word with < 3 letters
            if len(value.split()) == 1 and len(value) < 3:
                extracted[key] = "Not found"
            else:
                extracted[key] = value
        else:
            extracted[key] = "Not found"
    return extracted


# Code 2 Functions
class ImgCorrect:
    def __init__(self, img):
        self.img = img
        self.h, self.w, self.channel = self.img.shape
        if self.w <= self.h:
            self.scale = 700 / self.w
            self.img = cv2.resize(self.img, (0, 0), fx=self.scale, fy=self.scale, interpolation=cv2.INTER_NEAREST)
        else:
            self.scale = 700 / self.h
            self.img = cv2.resize(self.img, (0, 0), fx=self.scale, fy=self.scale, interpolation=cv2.INTER_NEAREST)
        self.gray = cv2.cvtColor(self.img, cv2.COLOR_BGR2GRAY)

    def img_lines(self):
        ret, binary = cv2.threshold(self.gray, 0, 255, cv2.THRESH_BINARY_INV | cv2.THRESH_OTSU)
        kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (3, 3))
        binary = cv2.dilate(binary, kernel)
        edges = cv2.Canny(binary, 50, 200)
        self.lines = cv2.HoughLinesP(edges, 1, np.pi / 180, 100, minLineLength=100, maxLineGap=20)
        if self.lines is None:
            return None
        lines1 = self.lines[:, 0, :]
        imglines = self.img.copy()
        for x1, y1, x2, y2 in lines1[:]:
            cv2.line(imglines, (x1, y1), (x2, y2), (0, 255, 0), 3)
        return imglines

    def search_lines(self):
        lines = self.lines[:, 0, :]
        number_inexist_k = 0
        sum_pos_k45 = number_pos_k45 = 0
        sum_pos_k90 = number_pos_k90 = 0
        sum_neg_k45 = number_neg_k45 = 0
        sum_neg_k90 = number_neg_k90 = 0
        sum_zero_k = number_zero_k = 0

        for x in lines:
            if x[2] == x[0]:
                number_inexist_k += 1
                continue
            degree = degrees(atan((x[3] - x[1]) / (x[2] - x[0])))
            if 0 < degree < 45:
                number_pos_k45 += 1
                sum_pos_k45 += degree
            if 45 <= degree < 90:
                number_pos_k90 += 1
                sum_pos_k90 += degree
            if -45 < degree < 0:
                number_neg_k45 += 1
                sum_neg_k45 += degree
            if -90 < degree <= -45:
                number_neg_k90 += 1
                sum_neg_k90 += degree
            if x[3] == x[1]:
                number_zero_k += 1

        max_number = max(number_inexist_k, number_pos_k45, number_pos_k90, number_neg_k45, number_neg_k90,
                         number_zero_k)

        if max_number == number_inexist_k:
            return 90
        if max_number == number_pos_k45:
            return sum_pos_k45 / number_pos_k45
        if max_number == number_pos_k90:
            return sum_pos_k90 / number_pos_k90
        if max_number == number_neg_k45:
            return sum_neg_k45 / number_neg_k45
        if max_number == number_neg_k90:
            return sum_neg_k90 / number_neg_k90
        if max_number == number_zero_k:
            return 0

    def rotate_image(self, degree):
        if -45 <= degree <= 0:
            degree = degree
        if -90 <= degree < -45:
            degree = 90 + degree
        if 0 < degree <= 45:
            degree = degree
        if 45 < degree <= 90:
            degree = degree - 90
        height, width = self.img.shape[:2]
        heightNew = int(width * fabs(sin(radians(degree))) + height * fabs(cos(radians(degree))))
        widthNew = int(height * fabs(sin(radians(degree))) + width * fabs(cos(radians(degree))))
        matRotation = cv2.getRotationMatrix2D((width / 2, height / 2), degree, 1)
        matRotation[0, 2] += (widthNew - width) / 2
        matRotation[1, 2] += (heightNew - height) / 2
        imgRotation = cv2.warpAffine(self.img, matRotation, (widthNew, heightNew), borderValue=(255, 255, 255))
        return imgRotation


def dskew(img):
    bg_color = [255, 255, 255]
    pad_img = cv2.copyMakeBorder(img, 100, 100, 100, 100, cv2.BORDER_CONSTANT, value=bg_color)
    imgcorrect = ImgCorrect(pad_img)
    lines_img = imgcorrect.img_lines()
    if lines_img is None:
        rotate = imgcorrect.rotate_image(0)
    else:
        degree = imgcorrect.search_lines()
        rotate = imgcorrect.rotate_image(degree)
    return rotate


def preprocess_before_crop(scan_path):
    original_image = scan_path
    gray = cv2.cvtColor(original_image, cv2.COLOR_BGR2GRAY)
    kernel = np.array([[0, -1, 0], [-1, 5, -1], [0, -1, 0]])
    sharpened = cv2.filter2D(gray, -1, kernel)
    bilateral_filtered = cv2.bilateralFilter(sharpened, d=9, sigmaColor=75, sigmaSpace=75)
    clahe = cv2.createCLAHE(clipLimit=2.0, tileGridSize=(8, 8))
    contrast = clahe.apply(bilateral_filtered)
    blurred = cv2.GaussianBlur(contrast, (3, 3), 0)
    adaptive_thresh = cv2.adaptiveThreshold(blurred, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C, cv2.THRESH_BINARY, 11, 2)
    return gray, original_image


def get_tesseract_ocr(image):
    img = Image.fromarray(image)
    ocr_text = pytesseract.image_to_string(img, lang='ben+eng')
    return ocr_text

def get_paddle_ocr(image):
    results = ocr.ocr(image_path, cls=True)
    all_text = ""
    for line in results[0]:
        text = line[1][0]
        all_text += text + "\n"
    return all_text


def get_easyocr_text(image_path):
    # results = reader.readtext(image_path)
    # ocr_text = "\n".join([text for _, text, _ in results])
    # return ocr_text
    return ''


def contains_english(text):
    if not text or text == "Not found":
        return False
    return bool(re.search(r'[a-zA-Z]', text))


def contains_bangla(text):
    if not text or text == "Not found":
        return False
    return bool(re.search(r'[\u0980-\u09FF]', text))


def clean_ocr_text(text):
    keywords_to_remove = [
        r"গণপ্রজাতন্ত্রী বাংলাদেশ সরকার", r"গণপ্রজাতন্ত্রী সরকার", r"গণপ্রজাতন্ত্রী",
        r"বাংলাদেশ সরকার", r"Government of the People", r"National ID Card",
        r"জাতীয় পরিচয় পত্র", r"জাতীয় পরিচয়", r"20/05/2025 09:12",r"জাতীয় পরিচয্ন পত্র"
    ]
    dob_pattern = r"(Date of Birth|DOB|Date|Birth)[:：]?\s*(\d{1,2}\s*(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s*\d{4}|\d{1,2}[-/]\d{1,2}[-/]\d{4})"
    id_no_pattern = r"(ID\s*NO|NID\s*No\.?|NIDNo|NID\s*NO|NID\s*No|ID\s*N0)\s*[:：]?\s*([\d ]{8,30})"
    dob_matches, id_no_matches = [], []

    def store_dob(match):
        dob_matches.append(match.group(0))
        return f"__DOB_{len(dob_matches) - 1}__"

    def store_id_no(match):
        id_no_matches.append(match.group(0))
        return f"__ID_NO_{len(id_no_matches) - 1}__"

    text = re.sub(dob_pattern, store_dob, text, flags=re.IGNORECASE)
    text = re.sub(id_no_pattern, store_id_no, text, flags=re.IGNORECASE)
    lines = text.splitlines()
    cleaned_lines = []
    for line in lines:
        if not line.strip():
            continue
        for keyword in keywords_to_remove:
            line = re.sub(keyword, "", line, flags=re.IGNORECASE)
        line = re.sub(r"[\[\]\(\)\{\}0-9]{3,}", "", line)
        line = re.sub(r"\s+", " ", line).strip()
        if line:
            cleaned_lines.append(line)
    text = "\n".join(cleaned_lines)
    for i, dob in enumerate(dob_matches):
        text = text.replace(f"__DOB_{i}__", dob)
    for i, id_no in enumerate(id_no_matches):
        text = text.replace(f"__ID_NO_{i}__", id_no)
    return text


def merge_lines(text):
    lines = text.splitlines()
    merged_lines = []
    i = 0
    while i < len(lines):
        current_line = lines[i].strip()
        if re.match(r"^[^\x00-\x7F]+$", current_line) and len(current_line) < 10 and i + 1 < len(lines):
            next_line = lines[i + 1].strip()
            if (re.match(r"^[^\x00-\x7F]+$", next_line) and
                    not any(re.search(pattern, next_line, re.IGNORECASE) for pattern in fields_code2.values())):
                merged_lines.append(current_line + " " + next_line)
                i += 2
                continue
        if re.match(r"^\d{1,2}$", current_line) and i + 1 < len(lines) and re.match(r"^\d{4}$", lines[i + 1]):
            merged_lines.append(current_line + " Jan " + lines[i + 1])
            i += 2
            continue
        merged_lines.append(current_line)
        i += 1
    return "\n".join(merged_lines)


def clean_bangla_name(name):
    if not name or name == "Not found":  # Condition 2: Explicitly check empty
        return "Not found"
    cleaned = re.sub(r"[^\u0980-\u09FF\s]", "", name).strip()
    cleaned = re.sub(r"\s+", " ", cleaned).strip()
    # Condition 1: Check for single word with < 3 letters
    if len(cleaned.split()) == 1 and len(cleaned) < 3:
        return "Not found"
    return cleaned if cleaned else "Not found"


def clean_english_name(name):
    if not name or name == "Not found":  # Condition 2: Explicitly check empty
        return "Not found"
    cleaned = re.sub(r"[^A-Za-z\s\.]", "", name).strip()
    cleaned = re.sub(r"\s+", " ", cleaned).strip()
    # Condition 1: Check for single word with < 3 letters
    if len(cleaned.split()) == 1 and len(cleaned) < 3:
        return "Not found"
    return cleaned if cleaned else "Not found"


def clean_date_of_birth(date):
    if not date or date == "Not found":  # Condition 2: Explicitly check empty
        return "Not found"
    cleaned = re.sub(r"[^0-9A-Za-z\s\-/]", "", date).strip()
    cleaned = re.sub(r"\s+", " ", cleaned).strip()
    if re.match(
            r"^\d{1,2}\s*(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s*\d{4}$|^\d{1,2}[-/]\d{1,2}[-/]\d{4}$",
            cleaned, re.IGNORECASE):
        year = int(re.search(r"\d{4}", cleaned).group())
        if 1900 <= year <= 2025:
            return cleaned
    return "Invalid"


def clean_id_no(id_no):
    if not id_no or id_no == "Not found":  # Condition 2: Explicitly check empty
        return "Not found"
    cleaned = re.sub(r"[^0-9]", "", id_no).strip()
    if re.match(r"^\d{10}$|^\d{13}$|^\d{17}$", cleaned):
        return cleaned
    return "Invalid"


def extract_fields_code2(text):
    extracted = {key: "Not found" for key in fields_code2}
    text = clean_ocr_text(text)
    text = merge_lines(text)

    # Regex-based extraction
    for key, pattern in fields_code2.items():
        match = re.search(pattern, text, re.IGNORECASE)
        if match:
            value = match.group(1).strip()
            # Condition 2: Skip if value is empty
            if not value:
                extracted[key] = "Not found"
                continue
            # Condition 1: Check for single word with < 3 letters
            if len(value.split()) == 1 and len(value) < 3:
                extracted[key] = "Not found"
            else:
                extracted[key] = value

    # Line-based inference for remaining fields
    lines = text.splitlines()
    name_index = -1
    for i, line in enumerate(lines):
        line = line.strip()
        # Condition 2: Skip empty lines
        if not line:
            continue
        # Condition 1: Skip single-word lines with < 3 letters
        if len(line.split()) == 1 and len(line) < 3:
            continue
        # Skip lines that match any pattern to avoid reprocessing
        if not any(re.search(pattern, line, re.IGNORECASE) for pattern in fields_code2.values()):
            if re.match(r"[^\x00-\x7F]+", line) and extracted["নাম"] == "Not found":
                extracted["নাম"] = line
                name_index = i
            elif re.match(r"[A-Za-z\s\.]+", line) and extracted["Name"] == "Not found" and (
                    name_index == -1 or i > name_index):
                extracted["Name"] = line
            elif re.match(r"[^\x00-\x7F]+", line) and extracted["পিতা"] == "Not found":
                if (extracted["Name"] != "Not found" and i > lines.index(extracted["Name"]) if extracted[
                                                                                                   "Name"] in lines else True) or \
                        (extracted["নাম"] != "Not found" and i > lines.index(extracted["নাম"]) if extracted[
                                                                                                      "নাম"] in lines else i > name_index):
                    extracted["পিতা"] = line
            elif re.match(r"[^\x00-\x7F]+", line) and extracted["মাতা"] == "Not found" and extracted[
                "পিতা"] != "Not found":
                if (extracted["পিতা"] != "Not found" and i > lines.index(extracted["পিতা"]) if extracted[
                                                                                                   "পিতা"] in lines else True) or \
                        (extracted["Name"] != "Not found" and i > lines.index(extracted["Name"]) if extracted[
                                                                                                        "Name"] in lines else True) or \
                        (extracted["নাম"] != "Not found" and i > lines.index(extracted["নাম"]) if extracted[
                                                                                                      "নাম"] in lines else i > name_index):
                    extracted["মাতা"] = line

    # Apply cleaning functions
    extracted["নাম"] = clean_bangla_name(extracted["নাম"])
    extracted["পিতা"] = clean_bangla_name(extracted["পিতা"])
    extracted["মাতা"] = clean_bangla_name(extracted["মাতা"])
    extracted["স্বামী"] = clean_bangla_name(extracted["স্বামী"])
    extracted["স্ত্রী"] = clean_bangla_name(extracted["স্ত্রী"])
    extracted["Name"] = clean_english_name(extracted["Name"])
    extracted["DateOfBirth"] = clean_date_of_birth(extracted["DateOfBirth"])
    extracted["IDNO"] = clean_id_no(extracted["IDNO"])

    # Validate language-specific fields
    fields_to_validate = ['নাম', 'পিতা', 'মাতা', 'স্বামী', 'স্ত্রী']
    for field in fields_to_validate:
        if contains_english(extracted[field]):
            extracted[field] = "Not found"
    if contains_bangla(extracted["Name"]):
        extracted["Name"] = "Not found"

    return extracted


def remove_special_chars(text, field):
    if text == "Not found" or not text:
        return text
    if field == "DateOfBirth":
        cleaned = re.sub(r"[^0-9A-Za-z\s\-/\.]", "", text).strip()
        cleaned = re.sub(r"[-/]", " ", cleaned).strip()
        return re.sub(r"\s+", " ", cleaned).strip()
    if field == "IDNO":
        return re.sub(r"[^0-9]", "", text).strip()
    cleaned = re.sub(r"[^A-Za-z\s\.\u0980-\u09FF]", "", text).strip()
    return re.sub(r"\s+", " ", cleaned).strip()


def clean_date_field(text):
    if text == "Not found" or not text:
        return text
    # Define month mapping for standardization
    month_map = {
        'jan': 'Jan', 'feb': 'Feb', 'mar': 'Mar', 'apr': 'Apr',
        'may': 'May', 'jun': 'Jun', 'jul': 'Jul', 'aug': 'Aug',
        'sep': 'Sep', 'oct': 'Oct', 'nov': 'Nov', 'dec': 'Dec',
        'january': 'Jan', 'february': 'Feb', 'march': 'Mar', 'april': 'Apr',
        'may': 'May', 'june': 'Jun', 'july': 'Jul', 'august': 'Aug',
        'september': 'Sep', 'october': 'Oct', 'november': 'Nov', 'december': 'Dec'
    }
    # Pattern to match various date formats: DDMMMYYYY, DD MMM YYYY, DDMMM YYYY, DD-MMM-YYYY, DD/MM/YYYY
    date_pattern = r'(\d{1,2})\s*[-/\s]?([A-Za-z]+)\s*[-/\s]?(\d{4})'
    match = re.match(date_pattern, text.strip(), re.IGNORECASE)
    if match:
        day, month, year = match.groups()
        # Standardize month to first three letters
        month = month[:3].lower()
        if month in month_map:
            month = month_map[month]
        else:
            return "Invalid"
        current_year = datetime.now().year
        if not (1800 <= int(year) <= current_year - 18):
            return "Invalid"
        day = int(day)
        if not (1 <= day <= 31):
            return "Invalid"
        return f"{day} {month} {year}"
    return "Invalid"


def clean_all_special_chars(text):
    if text == "Not found" or not text:
        return text
    cleaned = re.sub(r"[^A-Za-z0-9\s\.\u0980-\u09FF]", "", text).strip()
    return re.sub(r"\s+", " ", cleaned).strip()


def clean_bangla_field(text):
    if text == "Not found" or not text:
        return text
    words = text.split()
    cleaned_words = [word for word in words if not re.search(r'[A-Za-z]', word)]
    cleaned = " ".join(cleaned_words).strip()
    return cleaned if cleaned else "Not found"


def clean_english_field(text):
    if text == "Not found" or not text:
        return text
    words = text.split()
    cleaned_words = [word for word in words if not re.search(r'[\u0980-\u09FF]', word)]
    cleaned = " ".join(cleaned_words).strip()
    cleaned = re.sub(r'\.(\w)', r'. \1', cleaned)
    return cleaned if cleaned else "Not found"


def process_output(e1, field):
    e1 = "Not found" if e1 == "No data found" else e1
    if not e1 or e1 == "":
        e1 = "Not found"

    if field == "DateOfBirth":
        e1 = clean_date_field(e1)

    e1_cleaned = clean_all_special_chars(e1)

    if field in ['নাম', 'পিতা', 'মাতা', 'স্বামী', 'স্ত্রী']:
        e1_cleaned = clean_bangla_field(e1_cleaned)
    elif field == "Name":
        e1_cleaned = clean_english_field(e1_cleaned)


    e1_cleaned = remove_special_chars(e1_cleaned, field)

    if e1_cleaned != "Not found" and len(e1_cleaned.split()) == 1 and len(e1_cleaned) < 3:
        return "Not found"

    return e1_cleaned


def process_image(image_path):
    # Code 1 Processing
    img = cv2.imread(image_path)
    paddle_text1 = get_paddle_ocr(img)
    #print(paddle_text1)
    paddle_text1 = clean_header_text(paddle_text1)
    paddle_results1 = extract_fields_code2(paddle_text1)
    paddle_results1 = infer_name_from_lines(paddle_text1, paddle_results1)


    final_results = {}
    for field in fields_code1:
        t1 = paddle_results1.get(field, "Not found")
        final_results[field] = process_output(t1,field)

    # print("\nFinal Combined Results:")
    # for field, value in final_results.items():
    #     print(f"{field}: {value}")

    return final_results


#Example Usage
# image_path = "C:/Users/ishfaq.rahman/Desktop/NID Images/New Images/NID_2.png"
# final_results = process_image(image_path)

try:
    final_results = process_image(image_path)
    print(json.dumps(final_results, ensure_ascii=False))
except Exception as e:
    print(json.dumps({"error": f"Script execution failed: {str(e)}"}))
    sys.exit(1)
