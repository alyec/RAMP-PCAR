﻿/* global define, console, $, i18n, RAMP, t */

define([
    /* Dojo */
    "dojo/_base/lang", "dojo/Deferred",

    /* Text */
    "dojo/text!./templates/filter_manager_template.json",

    /* Ramp */

    "utils/PopupManager", "ramp/dataLoader", "ramp/theme", "ramp/map", "ramp/layerLoader", "ramp/globalStorage", "ramp/stepItem",

    /* Util */
    "utils/util", "utils/tmplHelper", "utils/tmplUtil", "utils/array", "utils/dictionary", "utils/bricks"
],
    function (
        lang, Deferred,
        filter_manager_template,
        PopupManager, DataLoader, Theme, RampMap, LayerLoader, GlobalStorage, StepItem,
        UtilMisc, TmplHelper, TmplUtil, UtilArray, UtilDict, Bricks
    ) {
        "use strict";

        var rootNode = $("#searchMapSectionBody"),

            symbologyPreset = {},

            //steps,
            choiceTree,
            stepLookup = {},

            transitionDuration = 0.4;

        console.log(rootNode, symbologyPreset, transitionDuration, RAMP, UtilDict);

        choiceTree = {
            id: "sourceTypeStep",
            content: [
                {
                    id: "sourceType",
                    type: Bricks.ChoiceBrick,
                    config: {
                        header: "Data Source",
                        choices: [
                            {
                                key: "serviceTypeStep",
                                value: "Service"
                            },
                            {
                                key: "fileTypeStep",
                                value: "File"
                            }
                        ]
                    },
                    on: [
                            {
                                eventName: "change",
                                expose: { as: "advance" },
                                callback: function (step, data) {
                                    step.advance(data.selectedChoice);
                                }
                            }
                    ]
                }
            ],
            children: [
                {
                    id: "serviceTypeStep",
                    content: [
                        {
                            id: "serviceType",
                            type: Bricks.ChoiceBrick,
                            config: {
                                //template: "template_name", //optional, has a default
                                header: "Service Type",
                                choices: [
                                    {
                                        key: "feature",
                                        value: "Feature"
                                    },
                                    {
                                        key: "wms",
                                        value: "WMS"
                                    }
                                ]
                            }
                        },
                        {
                            id: "serviceURL",
                            type: Bricks.SimpleInputBrick,
                            config: {
                                //template: "template_name", //optional, has a default
                                header: "Service URL",
                                //label: "Service URL", // optional, equals to header by default
                                placeholder: "ESRI MapServer or Feature layer or WMS layer"
                            },
                            on: [
                                {
                                    eventName: "change",
                                    callback: function (step, data) {
                                        var value = data.inputValue,
                                            serviceTypeBrick = step.contentBricks.serviceType,
                                            guess = "";

                                        if (!serviceTypeBrick.isUserSelected()) {

                                            if (value.match(/ArcGIS\/rest\/services/ig)) {
                                                guess = "feature";
                                            } else if (value.match(/wms/ig)) {
                                                guess = "wms";
                                            }
                                            serviceTypeBrick.setChoice(guess);
                                        }
                                    }
                                }
                            ]
                        },
                        {
                            id: "serviceTypeOkCancel",
                            type: Bricks.OkCancelButtonBrick,
                            config: {
                                okLabel: "Load",
                                okButtonClass: "btn-primary",

                                cancelLabel: "Cancel",
                                cancelButtonClass: "btn-default btn-sm",
                                required: ["serviceType", "serviceURL"]
                            },
                            on: [
                                {
                                    eventName: "click",
                                    callback: function (step, data) {
                                        console.log("Just Click:", this, step, data);
                                    }
                                },
                                {
                                    eventName: "okClick",
                                    expose: { as: "advance" },
                                    callback: function (step, data) {
                                        console.log("Ok click:", this, step, data);
                                        step.advance("dummyButton");
                                    }
                                },
                                {
                                    eventName: "cancelClick",
                                    expose: { as: "retreat" },
                                    callback: function (step, data) {
                                        console.log("Cancel click:", this, step, data);
                                        step.retreat();
                                    }
                                }

                            ]
                        }
                    ],
                    children: [
                        {
                            id: "dummyButton",
                            content: [
                                {
                                    id: "okButton",
                                    type: Bricks.ButtonBrick,
                                    config: {
                                        //header: "test", //optional, omitted if not specified
                                        label: "Done" //optional, defaults to "Ok"
                                        //buttonClass: "btn-primary" //optional, defaults to "btn-primary"
                                    }
                                }
                            ]
                        }
                    ]
                },
                {
                    id: "fileTypeStep",
                    content: [
                        {
                            id: "fileType",
                            type: Bricks.ChoiceBrick,
                            config: {
                                //template: "template_name", //optional, has a default
                                header: "File Type",
                                choices: [
                                    {
                                        key: "feature",
                                        value: "GeoJSON"
                                    },
                                    {
                                        key: "wms",
                                        value: "CSV"
                                    },
                                    {
                                        key: "shapefile",
                                        value: "Shapefile"
                                    }
                                ]
                            }
                        },
                        {
                            id: "fileOrFileULR",
                            type: Bricks.FileInputBrick,
                            config: {
                                //template: "template_name", //optional, has a default
                                header: "File or URL",
                                //label: "Service URL", // optional, equals to header by default
                                placeholder: "Local file or URL"
                            },
                            on: [
                                {
                                    eventName: "change",
                                    callback: function (step, data) {
                                        console.log(this.id, this.isValid(), data);
                                    }
                                }
                            ]
                        },
                        {
                            id: "fileTypeOkCancel",
                            type: Bricks.OkCancelButtonBrick,
                            config: {
                                okLabel: "Load",
                                okButtonClass: "btn-primary",

                                cancelLabel: "Cancel",
                                cancelButtonClass: "btn-default btn-sm",
                                required: ["fileType", "fileOrFileULR"]
                            },
                            on: [
                                {
                                    eventName: "click",
                                    callback: function (step, data) {
                                        console.log("Just Click:", this, step, data);
                                    }
                                },
                                {
                                    eventName: "okClick",
                                    expose: { as: "advance" },
                                    callback: function (step, data) {
                                        console.log("Ok click:", this, step, data);
                                    }
                                },
                                {
                                    eventName: "cancelClick",
                                    expose: { as: "retreat" },
                                    callback: function (step, data) {
                                        console.log("Cancel click:", this, step, data);
                                    }
                                }

                            ]
                        }
                    ]
                }
            ]
        };

        //choiceTree = {
        //    id: "sourceTypeStep",
        //    content: "dfsf",
        //    children: [
        //        {

        //            id: "serviceTypeStep"
        //        },
        //        {
        //            id: "fileTypeStep"
        //        }
        //    ]
        //};

        function reset() {

            t.dfs(choiceTree, function (node, par/*, ctrl*/) {
                var stepItem,
                    level = par ? par.level + 1 : 1;

                node.level = level;
                stepItem = new StepItem(node);
                stepItem.on("currentStepChange", setCurrentStep);

                node.stepItem = stepItem;
                stepLookup[node.id] = stepItem;

                if (par) {
                    par.stepItem.addChild(stepItem);
                }

                console.log(node);
            });

            rootNode
                .find(".add-dataset-content")
                .append(stepLookup.sourceTypeStep.node)
            ;

            stepLookup.sourceTypeStep.currentStep(1);
        }

        function setCurrentStep(event) {
            t.dfs(choiceTree, function (node) {
                node.stepItem.currentStep(event.level, event.id);
            });
        }

        return {
            init: function () {
                reset();

                UtilDict.forEachEntry(GlobalStorage.DefaultRenderers,
                    function (key) {
                        symbologyPreset[key] = i18n.t("presets.defaultRenderers." + key);
                        //symbologyPreset[key] = value.title;
                    }
                );
            }
        };
    });