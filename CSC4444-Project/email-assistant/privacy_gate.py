import torch
from transformers import pipeline

class PrivacyGate:
    def __init__(self):
        print("Loading local privacy model...")
        self.classifier = pipeline(
            "zero-shot-classification", 
            model="facebook/bart-large-mnli", 
            device=-1 
        )
        self.candidate_labels = ["sensitive personal information", "generic newsletter", "general business"]

    def check_email(self, email_body):
        email_lower = email_body.lower()

        danger_zone = ['ssn', 'social security', 'credit card', 'password', 'bank account']
        if any(word in email_lower for word in danger_zone):
            return False

        whitelist = ['csc4444', 'project', 'deadline', 'assignment', 'submission']
        if any(word in email_lower for word in whitelist):
            return True

        result = self.classifier(email_body[:500], self.candidate_labels)
        top_label = result['labels'][0]
        score = result['scores'][0]

        if top_label == "sensitive personal information" and score > 0.90:
            return False
            
        return True