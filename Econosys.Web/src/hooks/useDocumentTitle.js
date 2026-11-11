import { useContext, useEffect, useId } from 'react';
import DocumentTitleContext from '../contexts/DocumentTitleContext';

const useDocumentTitle = (title) => {
    const { setPageTitle, clearPageTitle } = useContext(DocumentTitleContext);
    const key = useId();

    useEffect(() => {
        setPageTitle(key, title);
        return () => clearPageTitle(key);
    }, [clearPageTitle, key, setPageTitle, title]);
};

export default useDocumentTitle;
