import { initializeApp } from "firebase/app";
import { getAuth } from "firebase/auth";

const firebaseConfig = {
  apiKey: "AIzaSyAgO_fLJOgmuhzWLV9UmoOw6i1F4vWf2J8",
  authDomain: "seniordev-portfolio-84g5f.firebaseapp.com",
  projectId: "seniordev-portfolio-84g5f",
  storageBucket: "seniordev-portfolio-84g5f.firebasestorage.app",
  messagingSenderId: "999676318749",
  appId: "1:999676318749:web:b2d3eb8f014a414dae62f4"
};

const app = initializeApp(firebaseConfig);
export const auth = getAuth(app);
export default app;
