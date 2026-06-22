import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams, useBlocker } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';

import apiClient from '../../config/apiClient';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledInput from '../../components/LabeledInput';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import LabeledSwitch from '../../components/LabeledSwitch';
import { formatDateTime } from '../../helpers/dateUtils';
import { parseNullableInt } from '../../helpers/numberUtils';

const createNewProductModel = () => ({
    id: 0,
    name: '',
    noteInternal: '',
    noteExternal: '',
    noteExtra: '',
    materialId: null,
    materialThickness: '',
    lengthMm: null,
    widthMm: null,
    heightMm: null,
    format: '',
    nrOfColors: '',
    constructionId: null,
    varnishId: null,
    varnishOther: '',
    netWeightPer1000: null,
    productCode: '',
    oldDbId: null,
    materialGroup: null,
    active: true,
    isServicePackaging: false,
});

const renderProductMetaRow = (label, value, userName, userId) => {
    if (!value) {
        return null;
    }

    const actor = userName || (userId != null ? `Anv. ${userId}` : null);

    return (
        <div className="grid grid-cols-21 gap-1 mx-2">
            <div className="col-span-5"><span className="font-medium">{label}</span></div>
            <div className="col-span-8">{formatDateTime(value)}</div>
            <div className="col-span-8 text-gray-500">
                {actor && `av ${actor}`}
            </div>
        </div>
    );
};

const Product = () => {
    const navigate = useNavigate();
    const { id } = useParams();
    const isNewProduct = id === 'new';

    const [loading, setLoading] = useState(true);
    const [messages, setMessages] = useState([]);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);

    const [materials, setMaterials] = useState([]);
    const [constructions, setConstructions] = useState([]);
    const [varnishes, setVarnishes] = useState([]);

    const [product, setProduct] = useState(null);
    const [originalProduct, setOriginalProduct] = useState(null);
    const skipUnsavedCheckRef = useRef(false);

    const hasUnsavedChanges = useCallback(() => {
        if (skipUnsavedCheckRef.current) return false;
        if (!product || !originalProduct) return false;
        return JSON.stringify(product) !== JSON.stringify(originalProduct);
    }, [product, originalProduct]);

    const blocker = useBlocker(hasUnsavedChanges);

    useEffect(() => {
        const handleBeforeUnload = (event) => {
            if (!hasUnsavedChanges()) return;

            event.preventDefault();
            event.returnValue = '';
            return '';
        };

        window.addEventListener('beforeunload', handleBeforeUnload);
        return () => window.removeEventListener('beforeunload', handleBeforeUnload);
    }, [hasUnsavedChanges]);

    useEffect(() => {
        if (blocker.state === 'blocked') {
            setShowUnsavedWarning(true);
        }
    }, [blocker.state]);

    useEffect(() => {
        skipUnsavedCheckRef.current = false;
    }, [id]);

    const normalizedVarnishes = useMemo(
        () => [{ id: null, name: 'Ingen', isActive: true }, ...varnishes.filter((item) => item.isActive !== false)],
        [varnishes]
    );

    useEffect(() => {
        const controller = new AbortController();

        const loadLookupsAndProduct = async () => {
            setLoading(true);

            try {
                const [materialsRes, constructionsRes, varnishesRes] = await Promise.all([
                    apiClient.post('/materials/search', {
                        pagination: { pageNumber: 1, pageSize: 200 },
                        orderBy: [{ field: 'name', direction: 'asc' }],
                    }, { signal: controller.signal }),
                    apiClient.post('/constructions/search', {
                        pagination: { pageNumber: 1, pageSize: 200 },
                        orderBy: [{ field: 'name', direction: 'asc' }],
                    }, { signal: controller.signal }),
                    apiClient.get('/varnishes/search', { signal: controller.signal }),
                ]);

                const mappedMaterials = (materialsRes?.data?.items ?? []).map((item) => ({
                    id: item.id,
                    name: item.name,
                    isActive: item.active,
                }));

                const mappedConstructions = (constructionsRes?.data?.items ?? []).map((item) => ({
                    id: item.id,
                    name: item.name,
                    isActive: item.active,
                }));

                const mappedVarnishes = (varnishesRes?.data ?? []).map((item) => ({
                    id: item.id,
                    name: item.name,
                    isActive: item.active,
                }));

                setMaterials(mappedMaterials);
                setConstructions(mappedConstructions);
                setVarnishes(mappedVarnishes);

                if (isNewProduct) {
                    const newProduct = createNewProductModel();
                    setProduct(newProduct);
                    setOriginalProduct(structuredClone(newProduct));
                } else {
                    const productRes = await apiClient.get(`/products/${id}`, { signal: controller.signal });
                    const loadedProduct = productRes?.data ?? null;
                    setProduct(loadedProduct);
                    setOriginalProduct(structuredClone(loadedProduct));
                }
            } catch (error) {
                if (error.code === 'ERR_CANCELED') return;
                console.error('Failed to load product view data:', error);
                setMessages([{ type: 'error', text: 'Kunde inte lasa produktdata.' }]);
                setProduct((prev) => prev ?? (isNewProduct ? createNewProductModel() : null));
            } finally {
                if (!controller.signal.aborted) {
                    setLoading(false);
                }
            }
        };

        loadLookupsAndProduct();

        return () => controller.abort();
    }, [id, isNewProduct]);

    const handleBackClick = () => {
        if (window.history.length > 1) {
            navigate(-1);
            return;
        }

        navigate('/order/products');
    };

    const handleUnsavedWarningConfirm = () => {
        setShowUnsavedWarning(false);
        blocker.proceed();
    };

    const handleUnsavedWarningAbort = () => {
        setShowUnsavedWarning(false);
        blocker.reset();
    };

    const handleChange = (field, value) => {
        const resolvedValue = value?.target ? value.target.value : value;
        setProduct((prev) => ({
            ...prev,
            [field]: resolvedValue,
        }));
    };

    const buildPayload = (source) => {
        const lengthMm = parseNullableInt(source.lengthMm);
        const widthMm = parseNullableInt(source.widthMm);
        const heightMm = parseNullableInt(source.heightMm);

        const formattedDimensions = [lengthMm, widthMm, heightMm].every((x) => x !== null)
            ? `${lengthMm}x${widthMm}x${heightMm}`
            : source.format || null;

        return {
            name: source.name ?? '',
            noteInternal: source.noteInternal ?? '',
            noteExternal: source.noteExternal ?? '',
            noteExtra: source.noteExtra ?? '',
            materialId: parseNullableInt(source.materialId),
            materialThickness: source.materialThickness ?? '',
            lengthMm,
            widthMm,
            heightMm,
            format: formattedDimensions,
            nrOfColors: source.nrOfColors ?? '',
            constructionId: parseNullableInt(source.constructionId),
            varnishId: parseNullableInt(source.varnishId),
            varnishOther: source.varnishOther ?? '',
            netWeightPer1000: parseNullableInt(source.netWeightPer1000),
            productCode: source.productCode ?? '',
            oldDbId: parseNullableInt(source.oldDbId),
            materialGroup: parseNullableInt(source.materialGroup),
            active: Boolean(source.active),
            isServicePackaging: Boolean(source.isServicePackaging),
        };
    };

    const handleSave = async () => {
        if (!product) return;

        try {
            const payload = buildPayload(product);
            const productId = parseNullableInt(product.id);
            const isCreatingNew = !(productId && productId > 0);

            const response = isCreatingNew
                ? await apiClient.post('/products', payload)
                : await apiClient.put(`/products/${productId}`, payload);

            const savedProduct = response?.data ?? null;
            setProduct(savedProduct);
            setOriginalProduct(structuredClone(savedProduct));
            setMessages([
                { type: 'success', text: 'Produkten sparades.' },
            ]);

            if (isCreatingNew && savedProduct?.id) {
                skipUnsavedCheckRef.current = true;
                navigate(`/order/products/${savedProduct.id}`, { replace: true });
            }
        } catch (error) {
            console.error('Failed to save product:', error);
            setMessages([
                { type: 'error', text: 'Kunde inte spara produkten.' },
            ]);
        }
    };

    const handleDelete = async () => {
        if (!product?.id) return;

        setShowDeleteConfirm(false);

        try {
            await apiClient.delete(`/products/${product.id}`);
            navigate('/order/products');
        } catch (error) {
            console.error('Failed to delete product:', error);
            setMessages([
                { type: 'error', text: 'Kunde inte radera produkten.' },
            ]);
        }
    };

    if (loading) {
        return (
            <div className="h-full px-6 py-4">
                <Skeleton height={26} width={260} />
                <div className="mt-6 grid grid-cols-[320px_minmax(0,1fr)] gap-6">
                    <Skeleton height={420} />
                    <Skeleton height={420} />
                </div>
            </div>
        );
    }

    if (!product) {
        return (
            <div className="h-full px-6 py-10 text-sm text-gray-600">
                Produkten kunde inte hittas.
            </div>
        );
    }

    return (
        <div className="relative flex flex-col h-full">
            <ConfirmationModal
                isOpen={showDeleteConfirm}
                onClose={() => setShowDeleteConfirm(false)}
                onConfirm={handleDelete}
                title="RADERA PRODUKT"
                message={`Ar du säker på att du vill radera produkt ${product.productCode || product.id}? Åtgarden kan inte ångras.`}
                confirmText="Radera"
                cancelText="Avbryt"
                isDestructive={true}
            />

            <ConfirmationModal
                isOpen={showUnsavedWarning}
                onClose={handleUnsavedWarningAbort}
                onConfirm={handleUnsavedWarningConfirm}
                title="Osparande ändringar"
                message="Det finns ej sparande ändringar, vill du anda fortsatta?"
                confirmText="Fortsätt ändå"
                cancelText="Avbryt"
                isDestructive={false}
            />

            <h2 className="ml-90 text-sm pt-2 pb-2 text-gray-700">
                {product?.id ? (
                    <>Produkt <span className="ml-2 text-red-500">{product.productCode || product.id}</span></>
                ) : 'Ny produkt'}
            </h2>

            <div className="flex h-full items-stretch">

                {/* Left panel */}
                <div className="flex flex-col w-80 shrink-0 border-r border-gray-300 px-2 py-2 mb-5 mr-5">
                    <div className="space-y-3">
                        <h2 className="text-sm text-center text-gray-700">Info</h2>
                        <div className="space-y-2 text-xs text-gray-600">
                            {renderProductMetaRow('Skapad:', product?.createdAt, product?.createdByUserName, product?.createdBy)}
                            {renderProductMetaRow('Redigerad:', product?.editedAt, product?.editedByUserName, product?.editedBy)}
                        </div>
                    </div>

                    <hr className="mt-5 border-gray-300" />
                    <h2 className="text-sm text-center text-gray-700 mt-5">Meddelanden</h2>
                    {messages.length === 0 ? (
                        <p className="text-xs text-center font-light mt-4">Inga meddelanden</p>
                    ) : (
                        <ul className="mt-2 space-y-2">
                            {messages.map((message, index) => (
                                <li
                                    key={index}
                                    className={`text-center text-xs p-2 rounded border border-gray-200 ${message.type === 'error'
                                        ? 'bg-red-100 text-red-700'
                                        : message.type === 'warning'
                                            ? 'bg-yellow-100 text-yellow-800'
                                            : 'bg-green-100 text-green-700'
                                        }`}
                                >
                                    {message.text}
                                </li>
                            ))}
                        </ul>
                    )}
                </div>

                <div className="flex-grow ps-4 pe-10 py-2 max-w-350">
                    <div className="flex justify-between w-full mb-5">
                        <div className="flex items-center space-x-4">
                            <button
                                type="button"
                                onClick={handleBackClick}
                                className="shadow-md/30 text-xs text-white bg-gray-500 hover:bg-gray-700 px-5 p-[5px]"
                            >
                                Tillbaka
                            </button>
                            <button
                                type="button"
                                onClick={handleSave}
                                className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-5 p-[5px]"
                            >
                                Spara
                            </button>
                        </div>
                        <div className="flex items-center space-x-4">
                            {product?.id !== 0 && (
                                <button
                                    type="button"
                                    onClick={() => setShowDeleteConfirm(true)}
                                    className="shadow-md/30 text-xs text-white bg-red-700 hover:bg-red-800 px-5 p-[5px]"
                                >
                                    Radera
                                </button>
                            )}
                        </div>
                    </div>

                    <div className="mt-8 grid w-full grid-cols-[max-content_minmax(0,1fr)] gap-x-18">
                        <span>
                            <div className="grid grid-cols-[420px_350px] gap-x-16 gap-y-4">
                                <span>
                                    <LabeledInput
                                        label="Produktnr"
                                        value={product?.id || ''}
                                        disabled
                                        labelWidth="w-22"
                                        margintop="0"
                                    />
                                    <LabeledInput
                                        label="Namn"
                                        value={product?.name || ''}
                                        onChange={(value) => handleChange('name', value)}
                                        labelWidth="w-22"
                                        margintop="0"
                                    />
                                    <div className="mt-2">
                                        <div className="ml-2 mb-1 text-xs text-gray-700">Not. intern</div>
                                        <textarea
                                            value={product.noteInternal || ''}
                                            onChange={(event) => handleChange('noteInternal', event.target.value)}
                                            className="text-xs w-full border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white h-25"
                                        />
                                    </div>
                                    <div className="mt-2">
                                        <div className="ml-2 mb-1 text-xs text-gray-700">Notering, visas externt mot kund</div>
                                        <textarea
                                            value={product.noteExternal || ''}
                                            onChange={(event) => handleChange('noteExternal', event.target.value)}
                                            className="text-xs w-full border border-gray-300 rounded-sm px-2 py-1 focus:outline-none bg-white h-25"
                                        />
                                    </div>
                                    <LabeledReactSelect
                                        name="materialId"
                                        label="Material"
                                        value={product?.materialId || ''}
                                        items={materials}
                                        onChange={(value) => handleChange('materialId', value)}
                                        disableInactive
                                        labelWidth="w-22"
                                        margintop="2"
                                    />
                                    <LabeledReactSelect
                                        name="constructionId"
                                        label="Konstruktion"
                                        value={product?.constructionId || ''}
                                        items={constructions}
                                        onChange={(value) => handleChange('constructionId', value)}
                                        disableInactive
                                        labelWidth="w-22"
                                        margintop="0"
                                    />
                                    <LabeledInput
                                        label="Vikt/1000st"
                                        value={product?.netWeightPer1000 ?? ''}
                                        onChange={(value) => handleChange('netWeightPer1000', value)}
                                        labelWidth="w-22"
                                        margintop="0"
                                    />
                                    <LabeledInput
                                        label="Produktkod"
                                        value={product?.productCode || ''}
                                        onChange={(value) => handleChange('productCode', value)}
                                        labelWidth="w-22"
                                        margintop="0"
                                    />
                                    <LabeledSwitch
                                        field="active"
                                        name="active"
                                        label="Ar aktiv"
                                        value={Boolean(product?.active)}
                                        onChange={(_rowId, field, checked) => handleChange(field, checked)}
                                        labelWidth="w-22"
                                        marginTop={2}
                                    />
                                    <LabeledSwitch
                                        field="isServicePackaging"
                                        name="isServicePackaging"
                                        label="Ar serviceforp."
                                        value={Boolean(product?.isServicePackaging)}
                                        onChange={(_rowId, field, checked) => handleChange(field, checked)}
                                        labelWidth="w-22"
                                        marginTop={2}
                                    />
                                </span>

                                <span>
                                    <div className="flex items-center space-x-2 w-full text-xs pb-[1px] mt-0">
                                        <label className="w-24 flex-none text-xs text-gray-700">Format, lbh</label>
                                        <input
                                            type="text"
                                            value={product?.lengthMm ?? ''}
                                            onChange={(event) => handleChange('lengthMm', event.target.value)}
                                            className="w-24 text-xs px-2 py-1 border border-gray-300 bg-white rounded-sm focus:outline-none"
                                        />
                                        <input
                                            type="text"
                                            value={product?.widthMm ?? ''}
                                            onChange={(event) => handleChange('widthMm', event.target.value)}
                                            className="w-24 text-xs px-2 py-1 border border-gray-300 bg-white rounded-sm focus:outline-none"
                                        />
                                        <input
                                            type="text"
                                            value={product?.heightMm ?? ''}
                                            onChange={(event) => handleChange('heightMm', event.target.value)}
                                            className="w-24 text-xs px-2 py-1 border border-gray-300 bg-white rounded-sm focus:outline-none"
                                        />
                                    </div>
                                    <div className="flex items-center space-x-2 w-full text-xs pb-[1px] mt-1">
                                        <div className="w-24 flex-none" />
                                        <input
                                            type="text"
                                            value={product?.format || ''}
                                            onChange={(event) => handleChange('format', event.target.value)}
                                            className="flex-1 text-xs px-2 py-1 border border-gray-300 bg-white rounded-sm focus:outline-none"
                                            placeholder="Fri formatinformation"
                                        />
                                    </div>

                                    <LabeledInput
                                        label="Farger"
                                        value={product?.nrOfColors || ''}
                                        onChange={(value) => handleChange('nrOfColors', value)}
                                        labelWidth="w-24"
                                        margintop="2"
                                    />

                                    <div className="mt-8 grid grid-cols-[96px_1fr] gap-x-2 items-start">
                                        <div className="text-xs text-gray-700 pt-2">Lack</div>
                                        <div className="space-y-2">
                                            {normalizedVarnishes.map((varnish) => {
                                                const isChecked = (product.varnishId == null && varnish.id == null)
                                                    || Number(product.varnishId) === Number(varnish.id);

                                                return (
                                                    <label key={varnish.id ?? 'none'} className="flex items-center gap-2 text-xs text-gray-800">
                                                        <input
                                                            type="radio"
                                                            name="varnish"
                                                            checked={isChecked}
                                                            onChange={() => handleChange('varnishId', varnish.id)}
                                                            className="h-4 w-4"
                                                        />
                                                        <span>{varnish.name}</span>
                                                    </label>
                                                );
                                            })}
                                            <input
                                                type="text"
                                                value={product?.varnishOther || ''}
                                                onChange={(event) => handleChange('varnishOther', event.target.value)}
                                                placeholder="Annan lack"
                                                className="w-full text-xs px-2 py-1 border border-gray-300 bg-white rounded-sm focus:outline-none"
                                            />
                                        </div>
                                    </div>
                                </span>
                            </div>
                        </span>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default Product;
